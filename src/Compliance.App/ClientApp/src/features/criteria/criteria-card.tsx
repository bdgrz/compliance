import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import { Button, Card, CardContent, CardDescription, CardHeader, CardTitle, Spinner, Stack } from '@askrjs/themes/components';

import type { Program } from '../programs/programs.js';
import {
  categoryLabel,
  criteriaCategories,
  listCriteriaEditions,
  listCriteriaEntries,
  ProgramRequestError,
  selectCriteriaEdition,
  type CriteriaEdition,
} from './criteria.js';

function failureMessage(error: Error, subject: string) {
  if (error instanceof ProgramRequestError && error.status === 403) {
    return `You do not have permission to change the ${subject}.`;
  }
  if (error instanceof ProgramRequestError && error.status === 409 && !error.transient) {
    return 'Someone else changed this program since you opened it. Reload to see their changes, then choose again.';
  }
  return error.message;
}

function CriteriaBrowser({ edition }: { edition: CriteriaEdition }) {
  const [category, setCategory] = state('');
  const [kind, setKind] = state('');
  const entries = resource(
    () => listCriteriaEntries(edition.editionId, { category: category() || undefined, kind: kind() || undefined }),
    [edition.editionId, category(), kind()]
  );
  const list = entries.value ?? [];

  return (
    <section aria-labelledby="criteria-browse-heading">
      <h3 id="criteria-browse-heading">Browse {edition.editionLabel}</h3>
      <div className="criteria-filters">
        <label className="registration-field">
          <span>Category</span>
          <select value={category()} onChange={(event: Event) => setCategory((event.target as HTMLSelectElement).value)}>
            <option value="">All categories</option>
            {criteriaCategories.map((option) => (
              <option value={option.code}>{option.label}</option>
            ))}
          </select>
        </label>
        <label className="registration-field">
          <span>Kind</span>
          <select value={kind()} onChange={(event: Event) => setKind((event.target as HTMLSelectElement).value)}>
            <option value="">Criteria and points of focus</option>
            <option value="criterion">Criteria only</option>
            <option value="point_of_focus">Points of focus only</option>
          </select>
        </label>
      </div>
      {entries.pending && !entries.value ? (
        <Spinner label="Loading criteria" />
      ) : entries.error ? (
        <Stack gap="sm">
          <p role="alert">{entries.error.message}</p>
          <Button variant="secondary" onPress={() => entries.refresh()}>
            Try again
          </Button>
        </Stack>
      ) : list.length === 0 ? (
        <p>No criteria match these filters.</p>
      ) : (
        <ul className="plain-list criteria-entries">
          {list.map((entry) => (
            <li className={entry.kind === 'point_of_focus' ? 'criteria-point' : undefined}>
              <strong>{entry.identifier}</strong>
              <span className="criteria-meta">
                {' '}
                {categoryLabel(entry.category)} · {entry.kind === 'criterion' ? 'Criterion' : `Point of focus for ${entry.parentIdentifier ?? 'criterion'}`}
                {entry.sourceIdentifier ? ` · source ${entry.sourceIdentifier}` : ''}
              </span>
              <p>{entry.licensedText ?? entry.summary}</p>
              {entry.licensedText && entry.overlay ? (
                <p className="muted-text">
                  Licensed text supplied by {entry.overlay.supplier} ({entry.overlay.licenseReference}).
                </p>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function Provenance({ edition }: { edition: CriteriaEdition }) {
  const gapCategories = new Set(edition.supportGaps.map((gap) => gap.category));
  return (
    <Stack gap="sm">
      <p>
        {edition.framework} · {edition.editionLabel}, published {new Date(edition.publishedAt).toLocaleDateString()}.{' '}
        <a href={edition.sourceUrl} rel="noopener noreferrer" target="_blank">
          Source publication
        </a>
      </p>
      <p className="criteria-meta">{edition.contentRights}</p>
      <p>{edition.isComplete ? 'Complete catalog.' : 'Partial catalog.'} {edition.coverageNote}</p>
      <ul className="plain-list criteria-categories" aria-label="Category coverage">
        {criteriaCategories.map((category) => (
          <li>
            <strong>{category.label}</strong>
            {category.required ? ' (required in every boundary)' : ' (optional)'}
            {gapCategories.has(category.code) ? ' · has support gaps' : ' · supported'}
          </li>
        ))}
      </ul>
      {edition.supportGaps.length > 0 ? (
        <section aria-labelledby={`gaps-${edition.editionId}`}>
          <h3 id={`gaps-${edition.editionId}`}>Support gaps to review</h3>
          <ul className="plain-list criteria-gaps">
            {edition.supportGaps.map((gap) => (
              <li>
                <strong>{categoryLabel(gap.category)}</strong>: {gap.note}
              </li>
            ))}
          </ul>
        </section>
      ) : null}
    </Stack>
  );
}

export function CriteriaCard({ program, onChanged }: { program: Program; onChanged: () => void }) {
  const editions = resource(() => listCriteriaEditions(), []);
  const [choice, setChoice] = state<string | null>(null);
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  const isPending = pending();
  const error = actionError();
  const message = notice();
  const available = editions.value ?? [];
  const selectedId = choice() ?? program.criteriaEditionId ?? available[0]?.editionId ?? '';
  const selected = available.find((edition) => edition.editionId === selectedId) ?? null;
  const current = available.find((edition) => edition.editionId === program.criteriaEditionId) ?? null;

  async function save(event: Event) {
    event.preventDefault();
    if (!selectedId) return;
    setActionError(null);
    setNotice(null);
    setPending(true);
    try {
      await selectCriteriaEdition(program.programId, program.revision, selectedId);
      setNotice('Criteria catalog selected.');
      setChoice(null);
      onChanged();
    } catch (failure) {
      setActionError(failure instanceof Error ? failure : new Error('Unable to select the criteria catalog.'));
    } finally {
      setPending(false);
    }
  }

  let body: unknown;
  if (editions.pending && !editions.value) {
    body = <Spinner label="Loading criteria catalogs" />;
  } else if (editions.error) {
    const forbidden = editions.error instanceof ProgramRequestError && editions.error.status === 403;
    body = forbidden ? (
      <p>You do not have permission to view the criteria catalogs.</p>
    ) : (
      <Stack gap="sm">
        <p role="alert">{editions.error.message}</p>
        <Button variant="secondary" onPress={() => editions.refresh()}>
          Try again
        </Button>
      </Stack>
    );
  } else if (available.length === 0) {
    body = <p>No criteria catalogs are available yet.</p>;
  } else {
    body = (
      <Stack gap="md">
        <p>
          {current
            ? `This program traces to ${current.framework} · ${current.editionLabel}.`
            : 'No criteria catalog is selected for this program yet.'}
        </p>
        <form onSubmit={(event: Event) => void save(event)}>
          <Stack gap="sm">
            <label className="registration-field">
              <span>Criteria edition</span>
              <select value={selectedId} onChange={(event: Event) => setChoice((event.target as HTMLSelectElement).value)}>
                {available.map((edition) => (
                  <option value={edition.editionId}>
                    {edition.framework} · {edition.editionLabel}
                  </option>
                ))}
              </select>
            </label>
            {error ? <p role="alert">{failureMessage(error, 'criteria catalog')}</p> : null}
            {message ? <p role="status">{message}</p> : null}
            <Button variant="primary" type="submit" disabled={isPending || selectedId === program.criteriaEditionId}>
              {isPending ? 'Saving…' : 'Use this edition'}
            </Button>
          </Stack>
        </form>
        {selected ? <Provenance edition={selected} /> : null}
        {selected ? <CriteriaBrowser edition={selected} /> : null}
      </Stack>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Criteria catalog</CardTitle>
        <CardDescription>The SOC 2 criteria edition this program's controls trace to, with its source and coverage.</CardDescription>
      </CardHeader>
      <CardContent>{body}</CardContent>
    </Card>
  );
}
