import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import { Button, Card, CardContent, CardDescription, CardHeader, CardTitle, Spinner, Stack } from '@askrjs/themes/components';

import {
  acceptedReviewFor,
  approveBoundary,
  label,
  previewBoundaryImpact,
  ProgramRequestError,
  reviewBoundary,
  type BoundaryDecision,
  type BoundaryVersion,
} from '../boundaries.js';
import { type Conflict } from '../responsibilities.js';
import { describeBoundaryFailure } from './boundary-form.js';
import { conflictSentence } from './responsibilities-panel.js';

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

// Review and approval are personal decisions made here over authenticated HTTP. Standing
// separation-of-duties conflicts and the impact preview are shown before either action.
export function DecisionPanel({
  boundaryId,
  draft,
  decisions,
  conflicts,
  exceptionsPath,
  onDecided,
}: {
  boundaryId: string;
  draft: BoundaryVersion;
  decisions: BoundaryDecision[];
  conflicts: Conflict[];
  exceptionsPath: string;
  onDecided: () => void;
}) {
  const [previewVersion, setPreviewVersion] = state(0);
  const preview = resource(
    () => previewBoundaryImpact(boundaryId, draft.versionId, draft.revision),
    [boundaryId, draft.versionId, draft.revision, previewVersion()]
  );
  const [outcome, setOutcome] = state<'accept' | 'request_changes'>('accept');
  const [reviewRationale, setReviewRationale] = state('');
  const [reviewWaiver, setReviewWaiver] = state('');
  const [effectiveFrom, setEffectiveFrom] = state(today());
  const [approvalRationale, setApprovalRationale] = state('');
  const [approvalWaiver, setApprovalWaiver] = state('');
  const [acknowledged, setAcknowledged] = state<string | null>(null);
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  const accepted = acceptedReviewFor(decisions, draft);
  const impact = preview.value ?? undefined;
  const fresh = impact !== undefined && impact.revision === draft.revision;

  async function run(action: () => Promise<string>) {
    setActionError(null);
    setNotice(null);
    setPending(true);
    try {
      setNotice(await action());
      onDecided();
    } catch (failure) {
      const error = failure instanceof Error ? failure : new Error('The decision was not recorded.');
      if (error instanceof ProgramRequestError && error.status === 409) {
        // The draft or its downstream projections moved: require a fresh preview and acknowledgement.
        setAcknowledged(null);
        setPreviewVersion(previewVersion() + 1);
      }
      setActionError(error);
    } finally {
      setPending(false);
    }
  }

  function review(event: Event) {
    event.preventDefault();
    void run(async () => {
      await reviewBoundary(boundaryId, draft.versionId, draft.revision, outcome(), reviewRationale(), reviewWaiver().trim() || undefined);
      setReviewRationale('');
      return outcome() === 'accept' ? 'Review recorded: accepted.' : 'Review recorded: changes requested.';
    });
  }

  function approve(event: Event) {
    event.preventDefault();
    if (!accepted || !impact) return;
    void run(async () => {
      await approveBoundary(boundaryId, draft.versionId, {
        expectedRevision: draft.revision,
        acceptedReviewDecisionId: accepted.decisionId,
        effectiveFrom: effectiveFrom(),
        rationale: approvalRationale(),
        impactDigest: impact.digest,
        waiverId: approvalWaiver().trim() || undefined,
      });
      return `Boundary revision ${draft.revision} approved, effective ${effectiveFrom()}.`;
    });
  }

  const error = actionError();
  const canApprove = accepted !== null && fresh && impact.complete && acknowledged() === impact.digest;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Review and approval</CardTitle>
        <CardDescription>
          Decisions apply to draft revision {draft.revision} by {draft.author}. The author cannot review or approve their own
          draft without an approved exception.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Stack gap="md">
          <section className="boundary-conflicts-summary" aria-label="Separation-of-duties check">
            <h3>Separation-of-duties check</h3>
            {conflicts.length === 0 ? (
              <p>No conflicting responsibilities are assigned on this revision.</p>
            ) : (
              <div className="boundary-conflicts">
                <ul>
                  {conflicts.map((conflict) => (
                    <li>{conflictSentence(conflict)}</li>
                  ))}
                </ul>
                <a href={exceptionsPath}>Request or approve an exception</a>
              </div>
            )}
          </section>
          <section aria-label="Impact preview">
            <h3>Impact preview</h3>
            {preview.pending && !impact ? (
              <Spinner label="Loading impact preview" />
            ) : preview.error ? (
              <Stack gap="sm">
                <p role="alert">{preview.error.message}</p>
                <Button variant="secondary" onPress={() => setPreviewVersion(previewVersion() + 1)}>
                  Reload impact preview
                </Button>
              </Stack>
            ) : impact ? (
              <Stack gap="sm">
                {impact.changes.length === 0 ? (
                  <p>No scope changes from the approved version.</p>
                ) : (
                  <ul className="plain-list boundary-changes">
                    {impact.changes.map((change) => (
                      <li>
                        <strong>{label(change.changeType)}</strong> {label(change.field)}
                        {change.previous ? ` · was: ${change.previous}` : ''}
                        {change.proposed ? ` · proposed: ${change.proposed}` : ''}
                      </li>
                    ))}
                  </ul>
                )}
                <h4>Affected contexts</h4>
                {impact.contributions.length === 0 ? (
                  <p>No downstream records are affected.</p>
                ) : (
                  <ul className="plain-list boundary-contexts">
                    {impact.contributions.map((contribution) => (
                      <li>
                        <strong>{label(contribution.context)}</strong>: {contribution.records.length} affected record
                        {contribution.records.length === 1 ? '' : 's'}
                        {contribution.complete ? '' : ' (still being calculated)'}
                        {contribution.records.length > 0 ? (
                          <ul>
                            {contribution.records.map((record) => (
                              <li>
                                {label(record.recordType)}: {record.reason}
                              </li>
                            ))}
                          </ul>
                        ) : null}
                      </li>
                    ))}
                  </ul>
                )}
                {impact.complete ? null : (
                  <p role="status">
                    The impact preview is incomplete
                    {impact.pendingContexts.length > 0 ? ` (waiting on ${impact.pendingContexts.map(label).join(', ')})` : ''}.
                    Approval waits until every context reports.
                  </p>
                )}
                <Button variant="secondary" onPress={() => setPreviewVersion(previewVersion() + 1)}>
                  Reload impact preview
                </Button>
              </Stack>
            ) : null}
          </section>
          <form className="boundary-review" onSubmit={(event: Event) => review(event)}>
            <fieldset>
              <legend>Review revision {draft.revision}</legend>
              <Stack gap="sm">
                <label>
                  <input
                    type="radio"
                    name="boundary-review-outcome"
                    checked={outcome() === 'accept'}
                    onChange={() => setOutcome('accept')}
                  />{' '}
                  Accept
                </label>
                <label>
                  <input
                    type="radio"
                    name="boundary-review-outcome"
                    checked={outcome() === 'request_changes'}
                    onChange={() => setOutcome('request_changes')}
                  />{' '}
                  Request changes
                </label>
                <label className="registration-field">
                  <span>Review rationale</span>
                  <textarea
                    rows={2}
                    value={reviewRationale()}
                    onInput={(event: Event) => setReviewRationale((event.target as HTMLTextAreaElement).value)}
                    required
                  />
                </label>
                <label className="registration-field">
                  <span>Review exception ID (only with an approved exception)</span>
                  <input type="text" value={reviewWaiver()} onInput={(event: Event) => setReviewWaiver((event.target as HTMLInputElement).value)} />
                </label>
                <Button variant="primary" type="submit" disabled={pending()}>
                  Record review
                </Button>
              </Stack>
            </fieldset>
          </form>
          <form className="boundary-approve" onSubmit={(event: Event) => approve(event)}>
            <fieldset>
              <legend>Approve revision {draft.revision}</legend>
              <Stack gap="sm">
                {accepted ? (
                  <p>
                    Cites the accepted review by {accepted.actor} on {new Date(accepted.decidedAt).toLocaleDateString()}.
                  </p>
                ) : (
                  <p>Approval needs an accepted review of this revision first.</p>
                )}
                <label>
                  <input
                    type="checkbox"
                    checked={impact !== undefined && acknowledged() === impact.digest}
                    disabled={!fresh || !impact?.complete}
                    onChange={(event: Event) =>
                      setAcknowledged((event.target as HTMLInputElement).checked && impact ? impact.digest : null)
                    }
                  />{' '}
                  I reviewed the current impact preview and affected contexts
                </label>
                <label className="registration-field">
                  <span>Effective from</span>
                  <input
                    type="date"
                    value={effectiveFrom()}
                    onInput={(event: Event) => setEffectiveFrom((event.target as HTMLInputElement).value)}
                    required
                  />
                </label>
                <label className="registration-field">
                  <span>Approval rationale</span>
                  <textarea
                    rows={2}
                    value={approvalRationale()}
                    onInput={(event: Event) => setApprovalRationale((event.target as HTMLTextAreaElement).value)}
                    required
                  />
                </label>
                <label className="registration-field">
                  <span>Approval exception ID (only with an approved exception)</span>
                  <input type="text" value={approvalWaiver()} onInput={(event: Event) => setApprovalWaiver((event.target as HTMLInputElement).value)} />
                </label>
                <Button variant="primary" type="submit" disabled={pending() || !canApprove}>
                  Approve boundary
                </Button>
              </Stack>
            </fieldset>
          </form>
          {error ? <p role="alert">{describeDecisionFailure(error)}</p> : null}
          {notice() ? <p role="status">{notice()}</p> : null}
        </Stack>
      </CardContent>
    </Card>
  );
}

function describeDecisionFailure(error: Error): string {
  if (error instanceof ProgramRequestError && error.status === 403) {
    return `This decision was denied: ${error.message}`;
  }
  if (error instanceof ProgramRequestError && error.status === 409 && !error.transient) {
    return `${error.message} The impact preview was reloaded; review it and acknowledge it again.`;
  }
  return describeBoundaryFailure(error);
}
