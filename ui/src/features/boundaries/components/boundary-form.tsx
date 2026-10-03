import { state } from '@askrjs/askr';
import { Button, Stack } from '@askrjs/themes/components';

import {
  engagementStages,
  entryKinds,
  label,
  newScopeEntry,
  ProgramRequestError,
  subjectTypes,
  trustServicesCategories,
  type BoundaryContent,
  type ScopeEntry,
} from '../boundaries.js';

export function describeBoundaryFailure(error: Error, subject = 'this boundary'): string {
  if (error instanceof ProgramRequestError && error.status === 403) {
    return `You do not have permission to change ${subject}.`;
  }
  if (error instanceof ProgramRequestError && error.status === 409 && !error.transient) {
    return `Someone else changed ${subject} since you opened it (${error.message}). Reload to see their changes, then try again.`;
  }
  return error.message;
}

function inputValue(event: Event): string {
  return (event.target as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement).value;
}

// Shared by create, draft revision, and successor proposal. Governed inventory links carry a
// record ID; everything else stays an explicit unresolved reference until it is linked.
export function BoundaryForm({
  initial,
  submitLabel,
  onSubmit,
}: {
  initial: BoundaryContent;
  submitLabel: string;
  onSubmit: (content: BoundaryContent) => Promise<string | null>;
}) {
  const [content, setContent] = state<BoundaryContent>({
    ...initial,
    categories: [...initial.categories],
    entries: initial.entries.map((entry) => ({ ...entry })),
  });
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  function updateEntry(index: number, change: Partial<ScopeEntry>) {
    const entries = content().entries.map((entry, i) => (i === index ? { ...entry, ...change } : entry));
    setContent({ ...content(), entries });
  }

  function toggleCategory(category: string, checked: boolean) {
    const categories = checked
      ? [...content().categories, category]
      : content().categories.filter((value) => value !== category);
    setContent({ ...content(), categories: trustServicesCategories.filter((value) => categories.includes(value)) });
  }

  async function submit(event: Event) {
    event.preventDefault();
    setActionError(null);
    setNotice(null);
    setPending(true);
    try {
      setNotice(await onSubmit(content()));
    } catch (failure) {
      setActionError(failure instanceof Error ? failure : new Error('Unable to save the boundary.'));
    } finally {
      setPending(false);
    }
  }

  const error = actionError();
  const current = content();

  return (
    <form className="boundary-form" onSubmit={(event: Event) => void submit(event)}>
      <Stack gap="sm">
        <label className="registration-field">
          <span>System description statement</span>
          <textarea
            rows={4}
            value={current.statement}
            onInput={(event: Event) => setContent({ ...content(), statement: inputValue(event) })}
            required
          />
        </label>
        <label className="registration-field">
          <span>Engagement stage</span>
          <select
            value={current.engagementStage}
            onChange={(event: Event) => setContent({ ...content(), engagementStage: inputValue(event) })}
          >
            {engagementStages.map((stage) => (
              <option value={stage} selected={stage === current.engagementStage}>
                {label(stage)}
              </option>
            ))}
          </select>
        </label>
        <fieldset className="boundary-categories">
          <legend>Trust Services categories</legend>
          {trustServicesCategories.map((category) => (
            <label>
              <input
                type="checkbox"
                checked={current.categories.includes(category)}
                disabled={category === 'security'}
                onChange={(event: Event) => toggleCategory(category, (event.target as HTMLInputElement).checked)}
              />{' '}
              {label(category)}
              {category === 'security' ? ' (always required)' : ''}
            </label>
          ))}
        </fieldset>
        <h3>Scope entries</h3>
        {current.entries.length === 0 ? <p>No scope entries yet. Add services, people, systems, and assumptions.</p> : null}
        {current.entries.map((entry, index) => (
          <fieldset className="boundary-entry">
            <legend>
              Entry {index + 1}: {label(entry.kind)}
              {entry.subject ? ` · ${entry.subject}` : ''}
            </legend>
            <div className="boundary-entry-grid">
              <label className="registration-field">
                <span>Kind</span>
                <select value={entry.kind} onChange={(event: Event) => updateEntry(index, { kind: inputValue(event) })}>
                  {entryKinds.map((kind) => (
                    <option value={kind} selected={kind === entry.kind}>
                      {label(kind)}
                    </option>
                  ))}
                </select>
              </label>
              <label className="registration-field">
                <span>Subject type</span>
                <select
                  value={entry.subject_type}
                  onChange={(event: Event) => updateEntry(index, { subject_type: inputValue(event) })}
                >
                  {subjectTypes.map((type) => (
                    <option value={type} selected={type === entry.subject_type}>
                      {label(type)}
                    </option>
                  ))}
                </select>
              </label>
              <label className="registration-field">
                <span>Subject</span>
                <input
                  type="text"
                  value={entry.subject}
                  onInput={(event: Event) => updateEntry(index, { subject: inputValue(event) })}
                  required
                />
              </label>
              <label className="registration-field">
                <span>Owner</span>
                <input
                  type="text"
                  value={entry.owner_reference}
                  onInput={(event: Event) => updateEntry(index, { owner_reference: inputValue(event) })}
                  required
                />
              </label>
            </div>
            <label className="registration-field">
              <span>Rationale</span>
              <textarea
                rows={2}
                value={entry.rationale}
                onInput={(event: Event) => updateEntry(index, { rationale: inputValue(event) })}
                required
              />
            </label>
            <label>
              <input
                type="checkbox"
                checked={!entry.unresolved}
                onChange={(event: Event) => {
                  const linked = (event.target as HTMLInputElement).checked;
                  updateEntry(index, { unresolved: !linked, governed_record_id: linked ? entry.governed_record_id : null });
                }}
              />{' '}
              Linked to a governed inventory record
            </label>
            {entry.unresolved ? (
              <p className="boundary-unresolved">Unresolved reference: not yet linked to a governed record.</p>
            ) : (
              <label className="registration-field">
                <span>Governed record ID</span>
                <input
                  type="text"
                  value={entry.governed_record_id ?? ''}
                  onInput={(event: Event) => updateEntry(index, { governed_record_id: inputValue(event) })}
                  required
                />
              </label>
            )}
            <Button
              variant="secondary"
              type="button"
              onPress={() => setContent({ ...content(), entries: content().entries.filter((_, i) => i !== index) })}
            >
              Remove entry {index + 1}
            </Button>
          </fieldset>
        ))}
        <Button
          variant="secondary"
          type="button"
          onPress={() => setContent({ ...content(), entries: [...content().entries, newScopeEntry()] })}
        >
          Add scope entry
        </Button>
        {error ? <p role="alert">{describeBoundaryFailure(error)}</p> : null}
        {notice() ? <p role="status">{notice()}</p> : null}
        <Button variant="primary" type="submit" disabled={pending()}>
          {pending() ? 'Saving…' : submitLabel}
        </Button>
      </Stack>
    </form>
  );
}
