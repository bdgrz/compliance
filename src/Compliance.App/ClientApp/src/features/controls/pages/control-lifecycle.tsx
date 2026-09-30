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

import type { ControlContent } from '../controls.js';
import {
  approveControl,
  describeDecisionFailure,
  getEffectiveControlVersion,
  proposeControlRetirement,
  proposeControlSuccessor,
  retireControl,
  reviewControl,
  type ControlDecision,
  type ControlVersion,
  type DecisionInput,
  type ImpactPreview,
} from '../lifecycle.js';
import { ControlForm } from './control-form.js';

function today() {
  return new Date().toISOString().slice(0, 10);
}

function label(value: string) {
  return value.replaceAll('_', ' ');
}

function formDate(value: string) {
  return new Date(value).toLocaleDateString();
}

// Runs one lifecycle command, keeping its pending, failure, and success notice together.
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
      {failure ? <p role="alert">{describeDecisionFailure(failure)}</p> : null}
      {notice ? <p role="status">{notice}</p> : null}
    </>
  );
}

function DecisionFields({
  prefix,
  value,
  onChange,
}: {
  prefix: string;
  value: DecisionInput;
  onChange: (next: DecisionInput) => void;
}) {
  return (
    <>
      <label className="registration-field">
        <span>Rationale</span>
        <textarea
          rows={3}
          maxLength={4000}
          value={value.rationale}
          onInput={(event: Event) =>
            onChange({
              ...value,
              rationale: (event.target as HTMLTextAreaElement).value,
            })
          }
          required
        />
      </label>
      <label className="registration-field">
        <span>Separation-of-duties waiver ID (optional)</span>
        <input
          type="text"
          value={value.waiverId}
          aria-describedby={`${prefix}-waiver-hint`}
          onInput={(event: Event) =>
            onChange({
              ...value,
              waiverId: (event.target as HTMLInputElement).value,
            })
          }
        />
        <small id={`${prefix}-waiver-hint`}>
          The author of a proposal cannot decide it themselves. Cite an active
          waiver scoped to this exact revision only when your organization
          granted one.
        </small>
      </label>
    </>
  );
}

const emptyDecision: DecisionInput = { rationale: '', waiverId: '' };

export function ReviewForm({
  programId,
  controlId,
  revision,
  subject,
  onDecided,
}: {
  programId: string;
  controlId: string;
  revision: number;
  subject: string;
  onDecided: () => void;
}) {
  const [outcome, setOutcome] = state<'accept' | 'request_changes'>('accept');
  const [input, setInput] = state<DecisionInput>(emptyDecision);
  const action = useAction();

  function submit(event: Event) {
    event.preventDefault();
    void action.run(async () => {
      await reviewControl(programId, controlId, revision, outcome(), input());
      setInput(emptyDecision);
      onDecided();
      return outcome() === 'accept'
        ? `Accepted revision ${revision}.`
        : `Requested changes on revision ${revision}.`;
    });
  }

  return (
    <form onSubmit={submit} aria-label={`Review ${subject}`}>
      <Stack gap="sm">
        <fieldset className="control-outcome">
          <legend>Review outcome for revision {revision}</legend>
          <label>
            <input
              type="radio"
              name="control-review-outcome"
              checked={outcome() === 'accept'}
              onChange={() => setOutcome('accept')}
            />{' '}
            Accept
          </label>
          <label>
            <input
              type="radio"
              name="control-review-outcome"
              checked={outcome() === 'request_changes'}
              onChange={() => setOutcome('request_changes')}
            />{' '}
            Request changes
          </label>
        </fieldset>
        <DecisionFields
          prefix="control-review"
          value={input()}
          onChange={setInput}
        />
        <Feedback failure={action.failure()} notice={action.notice()} />
        <Button variant="primary" type="submit" disabled={action.pending()}>
          {action.pending() ? 'Recording…' : 'Record review'}
        </Button>
      </Stack>
    </form>
  );
}

function AcceptedReviewNote({
  review,
  revision,
}: {
  review: ControlDecision | null;
  revision: number;
}) {
  return review ? (
    <p>
      Cites the accepted review by {review.actor} on{' '}
      {formDate(review.decidedAt)}
      {review.waived ? ' (separation of duties waived)' : ''}:{' '}
      {review.rationale}
    </p>
  ) : (
    <p className="control-meta">
      A decision needs the latest review of revision {revision} to accept it
      first.
    </p>
  );
}

export function ApproveForm({
  programId,
  controlId,
  revision,
  ownerResolution,
  review,
  preview,
  onDecided,
}: {
  programId: string;
  controlId: string;
  revision: number;
  ownerResolution: string;
  review: ControlDecision | null;
  preview: ImpactPreview | null;
  onDecided: () => void;
}) {
  const [effectiveFrom, setEffectiveFrom] = state(today());
  const [input, setInput] = state<DecisionInput>(emptyDecision);
  const action = useAction();
  const successor = preview !== null;
  const blocked = review === null || (successor && !preview.complete);

  function submit(event: Event) {
    event.preventDefault();
    if (review === null) return;
    void action.run(async () => {
      await approveControl(
        programId,
        controlId,
        revision,
        review.decisionId,
        effectiveFrom(),
        preview?.digest ?? null,
        input()
      );
      onDecided();
      return `Approved revision ${revision}; it takes effect ${effectiveFrom()}.`;
    });
  }

  return (
    <form onSubmit={submit} aria-label="Approve control">
      <Stack gap="sm">
        <AcceptedReviewNote review={review} revision={revision} />
        <p className="control-meta">
          Owner: {label(ownerResolution)}. The owner named in the draft is only
          a declaration. Approval verifies that an active client-personnel
          member is assigned as control owner on this exact revision, and fails
          with an explanation if not.
        </p>
        {successor ? (
          <p className="control-meta">
            {preview.complete
              ? `Approving acknowledges impact preview ${preview.digest.slice(0, 12)}.`
              : 'The impact preview is incomplete; approval waits until every context reports.'}
          </p>
        ) : null}
        <label className="registration-field">
          <span>Effective from</span>
          <input
            type="date"
            value={effectiveFrom()}
            onInput={(event: Event) =>
              setEffectiveFrom((event.target as HTMLInputElement).value)
            }
            required
          />
        </label>
        <DecisionFields
          prefix="control-approve"
          value={input()}
          onChange={setInput}
        />
        <Feedback failure={action.failure()} notice={action.notice()} />
        <Button
          variant="primary"
          type="submit"
          disabled={action.pending() || blocked}
        >
          {action.pending()
            ? 'Approving…'
            : successor
              ? 'Approve successor'
              : 'Approve and activate'}
        </Button>
      </Stack>
    </form>
  );
}

export function RetireForm({
  programId,
  controlId,
  revision,
  review,
  preview,
  onDecided,
}: {
  programId: string;
  controlId: string;
  revision: number;
  review: ControlDecision | null;
  preview: ImpactPreview;
  onDecided: () => void;
}) {
  const [input, setInput] = state<DecisionInput>(emptyDecision);
  const action = useAction();

  function submit(event: Event) {
    event.preventDefault();
    if (review === null) return;
    void action.run(async () => {
      await retireControl(
        programId,
        controlId,
        revision,
        review.decisionId,
        preview.digest,
        input()
      );
      onDecided();
      return 'Retirement approved. Earlier versions and their effective dates are kept.';
    });
  }

  return (
    <form onSubmit={submit} aria-label="Approve retirement">
      <Stack gap="sm">
        <AcceptedReviewNote review={review} revision={revision} />
        <p className="control-meta">
          {preview.complete
            ? `Approving acknowledges impact preview ${preview.digest.slice(0, 12)}.`
            : 'The impact preview is incomplete; approval waits until every context reports.'}
        </p>
        <DecisionFields
          prefix="control-retire"
          value={input()}
          onChange={setInput}
        />
        <Feedback failure={action.failure()} notice={action.notice()} />
        <Button
          variant="primary"
          type="submit"
          disabled={action.pending() || review === null || !preview.complete}
        >
          {action.pending() ? 'Retiring…' : 'Approve retirement'}
        </Button>
      </Stack>
    </form>
  );
}

export function ImpactPreviewPanel({
  preview,
  onRefresh,
}: {
  preview: ImpactPreview;
  onRefresh: () => void;
}) {
  return (
    <section
      aria-labelledby="control-impact-heading"
      className="control-impact"
    >
      <h3 id="control-impact-heading">
        Impact of this{' '}
        {preview.kind === 'retirement' ? 'retirement' : 'successor'}
      </h3>
      <p className="control-meta">
        Preview digest <code>{preview.digest}</code> ·{' '}
        {preview.complete ? 'complete' : 'incomplete'}
        {preview.pendingContexts.length > 0
          ? ` · still waiting on ${preview.pendingContexts.map(label).join(', ')}`
          : ''}
      </p>
      {preview.changes.length === 0 ? (
        <p>No field changes from the approved version.</p>
      ) : (
        <ul className="plain-list control-impact-changes">
          {preview.changes.map((change) => (
            <li>
              <strong>{label(change.field)}</strong> {label(change.changeType)}
              {change.previousValue !== null
                ? ` · was “${change.previousValue}”`
                : ''}
              {change.proposedValue !== null
                ? ` · becomes “${change.proposedValue}”`
                : ''}
            </li>
          ))}
        </ul>
      )}
      <ul className="plain-list control-impact-contexts">
        {preview.contributions.map((contribution) => (
          <li>
            <strong>{label(contribution.context)}</strong>:{' '}
            {label(contribution.status)} ({label(contribution.freshness)})
            {contribution.records.length > 0
              ? ` · ${contribution.records.length} affected: ${contribution.records.map((record) => `${label(record.recordType)} (${record.reason})`).join('; ')}`
              : ''}
          </li>
        ))}
      </ul>
      <Button variant="secondary" onPress={onRefresh}>
        Refresh impact preview
      </Button>
    </section>
  );
}

export function SuccessorForm({
  programId,
  controlId,
  current,
  onProposed,
}: {
  programId: string;
  controlId: string;
  current: ControlVersion;
  onProposed: () => void;
}) {
  async function propose(_identifier: string, content: ControlContent) {
    await proposeControlSuccessor(
      programId,
      controlId,
      current.versionId,
      content
    );
    onProposed();
    return 'Successor draft proposed. Review its impact before approving it.';
  }
  return (
    <ControlForm
      initial={current.content}
      withIdentifier={false}
      submitLabel="Propose successor"
      onSubmit={propose}
    />
  );
}

export function RetirementProposalForm({
  programId,
  controlId,
  current,
  onProposed,
}: {
  programId: string;
  controlId: string;
  current: ControlVersion;
  onProposed: () => void;
}) {
  const [effectiveUntil, setEffectiveUntil] = state('');
  const [rationale, setRationale] = state('');
  const action = useAction();

  function submit(event: Event) {
    event.preventDefault();
    void action.run(async () => {
      await proposeControlRetirement(
        programId,
        controlId,
        current.versionId,
        effectiveUntil(),
        rationale()
      );
      onProposed();
      return 'Retirement proposed. It needs an independent review and approval.';
    });
  }

  return (
    <form onSubmit={submit} aria-label="Propose retirement">
      <Stack gap="sm">
        <label className="registration-field">
          <span>Retire effective</span>
          <input
            type="date"
            value={effectiveUntil()}
            onInput={(event: Event) =>
              setEffectiveUntil((event.target as HTMLInputElement).value)
            }
            required
          />
        </label>
        <label className="registration-field">
          <span>Why retire this control</span>
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
        <Feedback failure={action.failure()} notice={action.notice()} />
        <Button variant="secondary" type="submit" disabled={action.pending()}>
          {action.pending() ? 'Proposing…' : 'Propose retirement'}
        </Button>
      </Stack>
    </form>
  );
}

function EffectiveLookup({
  programId,
  controlId,
}: {
  programId: string;
  controlId: string;
}) {
  const [on, setOn] = state(today());
  const effective = resource(
    () => getEffectiveControlVersion(programId, controlId, on()),
    [programId, controlId, on()]
  );
  return (
    <Stack gap="sm">
      <label className="registration-field">
        <span>Version effective on</span>
        <input
          type="date"
          value={on()}
          onInput={(event: Event) =>
            setOn((event.target as HTMLInputElement).value)
          }
        />
      </label>
      {effective.pending && !effective.value ? (
        <Spinner label="Loading effective version" />
      ) : effective.error ? (
        <p role="alert">{effective.error.message}</p>
      ) : effective.value ? (
        <p className="control-effective">
          Revision {effective.value.revision} ({effective.value.content.title})
          was in effect on {on()}.
        </p>
      ) : (
        <p className="control-effective">
          No approved version was in effect on {on()}.
        </p>
      )}
    </Stack>
  );
}

export function VersionsCard({
  programId,
  controlId,
  versions,
  decisions,
}: {
  programId: string;
  controlId: string;
  versions: ControlVersion[];
  decisions: ControlDecision[];
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Versions and decisions</CardTitle>
        <CardDescription>
          Approved versions are immutable; each keeps its effective dates after
          it is superseded or retired.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Stack gap="md">
          {versions.length === 0 ? (
            <p>No approved versions yet.</p>
          ) : (
            <ol className="plain-list control-versions">
              {versions.map((version) => (
                <li>
                  <strong>Revision {version.revision}</strong> ·{' '}
                  {label(version.status)} · effective {version.effectiveFrom}
                  {version.effectiveUntil
                    ? ` until ${version.effectiveUntil}`
                    : ''}{' '}
                  · approved by {version.approvedBy} on{' '}
                  {formDate(version.approvedAt)}
                  {version.waived ? ' (separation of duties waived)' : ''}
                  {version.predecessorVersionId ? ' · successor' : ''}
                </li>
              ))}
            </ol>
          )}
          <EffectiveLookup programId={programId} controlId={controlId} />
          <section aria-labelledby="control-decisions-heading">
            <h3 id="control-decisions-heading">Decisions</h3>
            {decisions.length === 0 ? (
              <p>No reviews or approvals recorded yet.</p>
            ) : (
              <ol className="plain-list control-decisions">
                {decisions.map((decision) => (
                  <li>
                    <strong>
                      {label(decision.kind)}: {label(decision.outcome)}
                    </strong>{' '}
                    on revision {decision.revision} by {decision.actor},{' '}
                    {formDate(decision.decidedAt)}
                    {decision.waived
                      ? ' (separation of duties waived)'
                      : ''} ·{' '}
                    {decision.rationale}
                  </li>
                ))}
              </ol>
            )}
          </section>
        </Stack>
      </CardContent>
    </Card>
  );
}
