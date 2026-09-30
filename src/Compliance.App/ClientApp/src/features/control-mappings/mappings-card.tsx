import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  Spinner,
  Stack,
} from '@askrjs/themes/components';

import { listCriteriaEntries } from '../criteria/criteria.js';
import { getProgram } from '../programs/programs.js';
import {
  describeMappingFailure,
  listControlMappings,
  pendingVersion,
  proposeControlMapping,
  ProgramRequestError,
  retireControlMapping,
  reviewControlMapping,
  type ControlMapping,
} from './mappings.js';

function label(value: string) {
  return value.replaceAll('_', ' ');
}

function useAction() {
  const [pending, setPending] = state(false);
  const [failure, setFailure] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);
  async function run(action: () => Promise<string>) {
    setFailure(null);
    setNotice(null);
    setPending(true);
    try {
      setNotice(await action());
    } catch (error) {
      setFailure(
        error instanceof Error ? error : new Error('The request failed.')
      );
    } finally {
      setPending(false);
    }
  }
  return { pending, failure, notice, run };
}

function Feedback({
  failure,
  notice,
}: {
  failure: Error | null;
  notice: string | null;
}) {
  return (
    <>
      {failure ? <p role="alert">{describeMappingFailure(failure)}</p> : null}
      {notice ? <p role="status">{notice}</p> : null}
    </>
  );
}

function ProposeMappingForm({
  programId,
  controlId,
  controlVersionId,
  editionId,
  mappings,
  onChanged,
}: {
  programId: string;
  controlId: string;
  controlVersionId: string;
  editionId: string;
  mappings: ControlMapping[];
  onChanged: () => void;
}) {
  const entries = resource(() => listCriteriaEntries(editionId), [editionId]);
  const [criterion, setCriterion] = state('');
  const [rationale, setRationale] = state('');
  const [explanation, setExplanation] = state('');
  const action = useAction();

  function submit(event: Event) {
    event.preventDefault();
    const identifier = criterion();
    if (!identifier) return;
    const existing = mappings.find(
      (mapping) => mapping.criterionIdentifier === identifier
    );
    void action.run(async () => {
      await proposeControlMapping(programId, {
        controlId,
        controlVersionId,
        editionId,
        criterionIdentifier: identifier,
        expectedRevision: existing?.revision ?? 0,
        rationale: rationale(),
        applicabilityExplanation: explanation(),
      });
      setRationale('');
      setExplanation('');
      onChanged();
      return `Proposed mapping to ${identifier}. It counts toward coverage only after review.`;
    });
  }

  if (entries.pending && !entries.value)
    return <Spinner label="Loading criteria" />;
  if (entries.error) {
    return (
      <Stack gap="sm">
        <p role="alert">{entries.error.message}</p>
        <Button variant="secondary" onPress={() => entries.refresh()}>
          Try again
        </Button>
      </Stack>
    );
  }

  return (
    <form onSubmit={submit} aria-label="Propose criteria mapping">
      <Stack gap="sm">
        <label className="registration-field">
          <span>Criterion</span>
          <select
            value={criterion()}
            onChange={(event: Event) =>
              setCriterion((event.target as HTMLSelectElement).value)
            }
            required
          >
            <option value="">Choose a criterion or point of focus</option>
            {(entries.value ?? []).map((entry) => (
              <option value={entry.identifier}>
                {entry.identifier} ·{' '}
                {entry.summary.length > 80
                  ? `${entry.summary.slice(0, 80)}…`
                  : entry.summary}
              </option>
            ))}
          </select>
        </label>
        <label className="registration-field">
          <span>Why this control addresses it</span>
          <textarea
            rows={3}
            maxLength={4000}
            value={rationale()}
            onInput={(event: Event) =>
              setRationale((event.target as HTMLTextAreaElement).value)
            }
            required
          />
        </label>
        <label className="registration-field">
          <span>Applicability explanation</span>
          <textarea
            rows={3}
            maxLength={4000}
            value={explanation()}
            aria-describedby="mapping-applicability-hint"
            onInput={(event: Event) =>
              setExplanation((event.target as HTMLTextAreaElement).value)
            }
            required
          />
          <small id="mapping-applicability-hint">
            Which systems, processes, or scope this mapping covers, and any
            exclusions.
          </small>
        </label>
        <Feedback failure={action.failure()} notice={action.notice()} />
        <Button variant="primary" type="submit" disabled={action.pending()}>
          {action.pending() ? 'Proposing…' : 'Propose mapping'}
        </Button>
      </Stack>
    </form>
  );
}

function MappingDecisions({
  programId,
  mapping,
  onChanged,
}: {
  programId: string;
  mapping: ControlMapping;
  onChanged: () => void;
}) {
  const [outcome, setOutcome] = state<'accept' | 'reject'>('accept');
  const [rationale, setRationale] = state('');
  const [waiverId, setWaiverId] = state('');
  const action = useAction();
  const pending = pendingVersion(mapping);
  const id = mapping.mappingId;

  function review(event: Event) {
    event.preventDefault();
    void action.run(async () => {
      await reviewControlMapping(
        programId,
        id,
        mapping.revision,
        outcome(),
        rationale(),
        waiverId()
      );
      setRationale('');
      onChanged();
      return outcome() === 'accept'
        ? `Accepted the mapping to ${mapping.criterionIdentifier}.`
        : `Rejected the mapping to ${mapping.criterionIdentifier}.`;
    });
  }

  function retire(event: Event) {
    event.preventDefault();
    void action.run(async () => {
      await retireControlMapping(programId, id, mapping.revision, rationale());
      setRationale('');
      onChanged();
      return `Retired the mapping to ${mapping.criterionIdentifier}. Its history is kept.`;
    });
  }

  if (pending) {
    return (
      <form
        onSubmit={review}
        aria-label={`Review mapping to ${mapping.criterionIdentifier}`}
      >
        <Stack gap="sm">
          <fieldset className="control-outcome">
            <legend>Review proposal {pending.versionNumber}</legend>
            <label>
              <input
                type="radio"
                name={`mapping-outcome-${id}`}
                checked={outcome() === 'accept'}
                onChange={() => setOutcome('accept')}
              />{' '}
              Accept
            </label>
            <label>
              <input
                type="radio"
                name={`mapping-outcome-${id}`}
                checked={outcome() === 'reject'}
                onChange={() => setOutcome('reject')}
              />{' '}
              Reject
            </label>
          </fieldset>
          <label className="registration-field">
            <span>Review rationale</span>
            <textarea
              rows={2}
              maxLength={4000}
              value={rationale()}
              onInput={(event: Event) =>
                setRationale((event.target as HTMLTextAreaElement).value)
              }
              required
            />
          </label>
          <label className="registration-field">
            <span>Separation-of-duties waiver ID (optional)</span>
            <input
              type="text"
              value={waiverId()}
              onInput={(event: Event) =>
                setWaiverId((event.target as HTMLInputElement).value)
              }
            />
          </label>
          <Feedback failure={action.failure()} notice={action.notice()} />
          <Button variant="primary" type="submit" disabled={action.pending()}>
            {action.pending() ? 'Recording…' : 'Record mapping review'}
          </Button>
        </Stack>
      </form>
    );
  }

  if (mapping.activeVersionNumber !== null) {
    return (
      <form
        onSubmit={retire}
        aria-label={`Retire mapping to ${mapping.criterionIdentifier}`}
      >
        <Stack gap="sm">
          <label className="registration-field">
            <span>Retirement rationale</span>
            <textarea
              rows={2}
              maxLength={4000}
              value={rationale()}
              onInput={(event: Event) =>
                setRationale((event.target as HTMLTextAreaElement).value)
              }
              required
            />
          </label>
          <Feedback failure={action.failure()} notice={action.notice()} />
          <Button variant="secondary" type="submit" disabled={action.pending()}>
            {action.pending() ? 'Retiring…' : 'Retire mapping'}
          </Button>
        </Stack>
      </form>
    );
  }

  return <Feedback failure={action.failure()} notice={action.notice()} />;
}

function MappingItem({
  programId,
  mapping,
  currentVersionId,
  onChanged,
}: {
  programId: string;
  mapping: ControlMapping;
  currentVersionId: string | null;
  onChanged: () => void;
}) {
  const stale =
    mapping.activeControlVersionId !== null &&
    currentVersionId !== null &&
    mapping.activeControlVersionId !== currentVersionId;
  return (
    <li className="control-mapping">
      <p>
        <strong>{mapping.criterionIdentifier}</strong> (
        {label(mapping.criterionKind)}) · {label(mapping.status)}
        {mapping.activeVersionNumber !== null
          ? ` · reviewed version ${mapping.activeVersionNumber}`
          : ' · no reviewed version'}
      </p>
      {stale ? (
        <p className="control-meta">
          The reviewed mapping cites an earlier approved control version.
        </p>
      ) : null}
      <details>
        <summary>History for {mapping.criterionIdentifier}</summary>
        <ol className="plain-list control-mapping-history">
          {mapping.versions.map((version) => (
            <li>
              <strong>Version {version.versionNumber}</strong> ·{' '}
              {label(version.status)} · proposed by {version.proposedBy} on{' '}
              {new Date(version.proposedAt).toLocaleDateString()}
              <br />
              Rationale: {version.rationale}
              <br />
              Applies to: {version.applicabilityExplanation}
              {version.reviewedAt ? (
                <>
                  <br />
                  Reviewed by {version.reviewedBy ?? 'unknown'} on{' '}
                  {new Date(version.reviewedAt).toLocaleDateString()}
                  {version.waived ? ' (separation of duties waived)' : ''}:{' '}
                  {version.reviewRationale}
                </>
              ) : null}
              {version.retiredAt ? (
                <>
                  <br />
                  Retired by {version.retiredBy ?? 'unknown'} on{' '}
                  {new Date(version.retiredAt).toLocaleDateString()}:{' '}
                  {version.retirementRationale}
                </>
              ) : null}
            </li>
          ))}
        </ol>
      </details>
      <MappingDecisions
        programId={programId}
        mapping={mapping}
        onChanged={onChanged}
      />
    </li>
  );
}

export function ControlMappingsCard({
  programId,
  controlId,
  currentVersionId,
}: {
  programId: string;
  controlId: string;
  currentVersionId: string | null;
}) {
  const [version, setVersion] = state(0);
  const program = resource(() => getProgram(programId), [programId]);
  const editionId = program.value?.criteriaEditionId ?? null;
  const mappings = resource(
    () =>
      editionId
        ? listControlMappings(programId, controlId, editionId)
        : Promise.resolve([]),
    [programId, controlId, editionId, version()]
  );
  const refresh = () => setVersion(version() + 1);

  let body: unknown;
  if (
    (program.pending && !program.value) ||
    (mappings.pending && !mappings.value)
  ) {
    body = <Spinner label="Loading criteria mappings" />;
  } else if (program.error || mappings.error) {
    const error = (program.error ?? mappings.error)!;
    body =
      error instanceof ProgramRequestError && error.status === 403 ? (
        <p>You do not have permission to view criteria mappings.</p>
      ) : (
        <Stack gap="sm">
          <p role="alert">{error.message}</p>
          <Button
            variant="secondary"
            onPress={() => {
              program.refresh();
              mappings.refresh();
            }}
          >
            Try again
          </Button>
        </Stack>
      );
  } else if (!editionId) {
    body = (
      <p>
        Select a criteria catalog on the program before mapping this control.
      </p>
    );
  } else {
    const list = mappings.value ?? [];
    body = (
      <Stack gap="md">
        {list.length === 0 ? (
          <p>This control is not mapped to any criteria yet.</p>
        ) : (
          <ul className="plain-list control-mappings">
            {list.map((mapping) => (
              <MappingItem
                programId={programId}
                mapping={mapping}
                currentVersionId={currentVersionId}
                onChanged={refresh}
              />
            ))}
          </ul>
        )}
        {currentVersionId ? (
          <section aria-labelledby="mapping-propose-heading">
            <h3 id="mapping-propose-heading">Propose a mapping</h3>
            <ProposeMappingForm
              programId={programId}
              controlId={controlId}
              controlVersionId={currentVersionId}
              editionId={editionId}
              mappings={list}
              onChanged={refresh}
            />
          </section>
        ) : (
          <p className="control-meta">
            Mappings cite an approved control version. Approve this control
            before proposing mappings.
          </p>
        )}
      </Stack>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Criteria mappings</CardTitle>
        <CardDescription>
          Which criteria of the program's selected edition this control
          addresses. Proposals count toward coverage only after an independent
          review; mapping never means a criterion is satisfied.
        </CardDescription>
      </CardHeader>
      <CardContent>{body}</CardContent>
    </Card>
  );
}
