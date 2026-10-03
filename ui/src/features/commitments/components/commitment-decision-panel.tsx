import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import { Button, Card, CardContent, CardDescription, CardHeader, CardTitle, Spinner, Stack } from '@askrjs/themes/components';

import { conflictSentence } from '../../boundaries/components/responsibilities-panel.js';
import { type Conflict } from '../../boundaries/responsibilities.js';
import {
  acceptedReviewFor,
  approveCommitmentDraft,
  describeCommitmentFailure,
  label,
  previewCommitmentImpact,
  ProgramRequestError,
  reviewCommitmentDraft,
  type CommitmentDecision,
  type CommitmentDraft,
} from '../commitments.js';

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

function inputValue(event: Event): string {
  return (event.target as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement).value;
}

// Review verifies owner, applicability, interpretation, and the exact source; approval is a separate
// decision by someone else that acknowledges the current impact preview. Both are HTTP-only.
export function CommitmentDecisionPanel({
  programId,
  draft,
  decisions,
  authors,
  conflicts,
  exceptionsPath,
  onDecided,
}: {
  programId: string;
  draft: CommitmentDraft;
  decisions: CommitmentDecision[];
  authors: string[];
  conflicts: Conflict[];
  exceptionsPath: string;
  onDecided: () => void;
}) {
  const [previewVersion, setPreviewVersion] = state(0);
  const preview = resource(
    () => previewCommitmentImpact(programId, draft.draftId, draft.revision),
    [programId, draft.draftId, draft.revision, previewVersion()]
  );
  const [outcome, setOutcome] = state<'accept' | 'request_changes'>('accept');
  const [reviewRationale, setReviewRationale] = state('');
  const [owner, setOwner] = state('');
  const [applicability, setApplicability] = state('applicable');
  const [interpretation, setInterpretation] = state('supported');
  const [interpretationNote, setInterpretationNote] = state('');
  const [verifiedReference, setVerifiedReference] = state('');
  const [sourceEvidence, setSourceEvidence] = state('');
  const [reviewWaiver, setReviewWaiver] = state('');
  const [effectiveFrom, setEffectiveFrom] = state(today());
  const [approvalRationale, setApprovalRationale] = state('');
  const [approvalWaiver, setApprovalWaiver] = state('');
  const [acknowledged, setAcknowledged] = state<string | null>(null);
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  const accepted = acceptedReviewFor(decisions, draft.revision);
  const effective = draft.status === 'effective';
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
        // The draft or a dependent context moved: require a fresh preview and acknowledgement.
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
      await reviewCommitmentDraft(programId, draft.draftId, draft.revision, {
        outcome: outcome(),
        rationale: reviewRationale(),
        ownerReference: owner(),
        applicability: applicability(),
        interpretation: interpretation(),
        interpretationNote: interpretationNote(),
        sourceVerifiedReference: verifiedReference(),
        sourceEvidence: sourceEvidence(),
        waiverId: reviewWaiver().trim() || undefined,
      });
      setReviewRationale('');
      return outcome() === 'accept'
        ? 'Review recorded: accepted. A different member must now approve it.'
        : 'Review recorded: changes requested.';
    });
  }

  function approve(event: Event) {
    event.preventDefault();
    if (!accepted || !impact) return;
    void run(async () => {
      await approveCommitmentDraft(programId, draft.draftId, {
        expectedRevision: draft.revision,
        acceptedReviewDecisionId: accepted.decisionId,
        effectiveFrom: effectiveFrom(),
        rationale: approvalRationale(),
        impactDigest: impact.digest,
        waiverId: approvalWaiver().trim() || undefined,
      });
      return `Revision ${draft.revision} approved, effective ${effectiveFrom()}.`;
    });
  }

  const error = actionError();
  const canApprove = accepted !== null && fresh && impact.complete && acknowledged() === impact.digest;
  const accepting = outcome() === 'accept';

  return (
    <Card>
      <CardHeader>
        <CardTitle>Review and approval</CardTitle>
        <CardDescription>
          Decisions apply to revision {draft.revision}. Review verifies the commitment; a separate approval makes it effective.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Stack gap="md">
          <section className="commitment-sod" aria-label="Separation-of-duties check">
            <h3>Separation-of-duties check</h3>
            <ul>
              <li>
                {authors.length === 0
                  ? 'No pending authors.'
                  : `Authored since the last effective version by ${authors.join(', ')}; they cannot review or approve it without an approved exception.`}
              </li>
              <li>
                {accepted
                  ? `${accepted.actor} accepted this revision, so they cannot also approve it without an approved exception.`
                  : 'The approver must be a different member from the accepted reviewer.'}
              </li>
              <li>When reviewers or approvers are assigned to this revision, only they may decide.</li>
            </ul>
            {conflicts.length > 0 ? (
              <div className="commitment-conflicts">
                <ul>
                  {conflicts.map((conflict) => (
                    <li>{conflictSentence(conflict)}</li>
                  ))}
                </ul>
              </div>
            ) : null}
            <a href={exceptionsPath}>Request or approve an exception</a>
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
                  <p>No changes from effective version {impact.effectiveVersion ?? 'none'}.</p>
                ) : (
                  <ul className="plain-list commitment-changes">
                    {impact.changes.map((change) => (
                      <li>
                        <strong>{label(change.field)}</strong>
                        {change.before ? ` · was: ${change.before}` : ' · new'}
                        {change.after ? ` · proposed: ${change.after}` : ''}
                      </li>
                    ))}
                  </ul>
                )}
                <h4>Dependents</h4>
                {impact.dependents.length === 0 ? (
                  <p>No boundaries or controls reference this commitment.</p>
                ) : (
                  <ul className="plain-list commitment-dependents">
                    {impact.dependents.map((dependent) => (
                      <li>
                        {label(dependent.recordType)} {dependent.recordId.slice(0, 8)} · {label(dependent.relationship)}
                      </li>
                    ))}
                  </ul>
                )}
                <h4>Contexts that cannot reference commitments</h4>
                <ul className="plain-list commitment-unlinked">
                  {impact.unlinked.map((item) => (
                    <li>
                      <strong>{label(item.context)}</strong>: {item.reason}
                    </li>
                  ))}
                </ul>
                {impact.complete ? null : <p role="status">The impact preview is incomplete. Approval waits until it completes.</p>}
                <Button variant="secondary" onPress={() => setPreviewVersion(previewVersion() + 1)}>
                  Reload impact preview
                </Button>
              </Stack>
            ) : null}
          </section>
          {effective ? (
            <p>Revision {draft.revision} is already effective. Revise the draft to propose a successor.</p>
          ) : (
            <>
              <form className="commitment-review" onSubmit={(event: Event) => review(event)}>
                <fieldset>
                  <legend>Review revision {draft.revision}</legend>
                  <Stack gap="sm">
                    <label>
                      <input type="radio" name="commitment-review-outcome" checked={accepting} onChange={() => setOutcome('accept')} />{' '}
                      Accept
                    </label>
                    <label>
                      <input
                        type="radio"
                        name="commitment-review-outcome"
                        checked={!accepting}
                        onChange={() => setOutcome('request_changes')}
                      />{' '}
                      Request changes
                    </label>
                    {accepting ? (
                      <>
                        <label className="registration-field">
                          <span>Verified owner</span>
                          <input type="text" value={owner()} onInput={(event: Event) => setOwner(inputValue(event))} required />
                        </label>
                        <label className="registration-field">
                          <span>Applicability</span>
                          <select value={applicability()} onChange={(event: Event) => setApplicability(inputValue(event))}>
                            <option value="applicable" selected={applicability() === 'applicable'}>
                              Applicable
                            </option>
                            <option value="not_applicable" selected={applicability() === 'not_applicable'}>
                              Not applicable
                            </option>
                          </select>
                        </label>
                        <label className="registration-field">
                          <span>Interpretation</span>
                          <select value={interpretation()} onChange={(event: Event) => setInterpretation(inputValue(event))}>
                            <option value="supported" selected={interpretation() === 'supported'}>
                              Supported by the source
                            </option>
                            <option value="unsupported" selected={interpretation() === 'unsupported'}>
                              Unsupported (stays visible on the version)
                            </option>
                          </select>
                        </label>
                        <label className="registration-field">
                          <span>Interpretation note</span>
                          <textarea
                            rows={2}
                            value={interpretationNote()}
                            onInput={(event: Event) => setInterpretationNote(inputValue(event))}
                          />
                        </label>
                        <label className="registration-field">
                          <span>Source reference you verified</span>
                          <input
                            type="text"
                            value={verifiedReference()}
                            onInput={(event: Event) => setVerifiedReference(inputValue(event))}
                            required
                          />
                        </label>
                        <p className="commitment-meta">Must match the draft's source reference exactly: {draft.sourceReference}</p>
                        <label className="registration-field">
                          <span>Source evidence checked</span>
                          <textarea
                            rows={2}
                            value={sourceEvidence()}
                            onInput={(event: Event) => setSourceEvidence(inputValue(event))}
                            required
                          />
                        </label>
                      </>
                    ) : null}
                    <label className="registration-field">
                      <span>Review rationale</span>
                      <textarea
                        rows={2}
                        value={reviewRationale()}
                        onInput={(event: Event) => setReviewRationale(inputValue(event))}
                        required
                      />
                    </label>
                    <label className="registration-field">
                      <span>Review exception ID (only with an approved exception)</span>
                      <input type="text" value={reviewWaiver()} onInput={(event: Event) => setReviewWaiver(inputValue(event))} />
                    </label>
                    <Button variant="primary" type="submit" disabled={pending()}>
                      Record review
                    </Button>
                  </Stack>
                </fieldset>
              </form>
              <form className="commitment-approve" onSubmit={(event: Event) => approve(event)}>
                <fieldset>
                  <legend>Approve revision {draft.revision}</legend>
                  <Stack gap="sm">
                    {accepted ? (
                      <p>
                        Cites the accepted review by {accepted.actor} on {new Date(accepted.decidedAt).toLocaleDateString()} (source{' '}
                        {label(accepted.sourceVerification)}).
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
                      I reviewed the current impact preview and dependents
                    </label>
                    <label className="registration-field">
                      <span>Effective from</span>
                      <input type="date" value={effectiveFrom()} onInput={(event: Event) => setEffectiveFrom(inputValue(event))} required />
                    </label>
                    <label className="registration-field">
                      <span>Approval rationale</span>
                      <textarea
                        rows={2}
                        value={approvalRationale()}
                        onInput={(event: Event) => setApprovalRationale(inputValue(event))}
                        required
                      />
                    </label>
                    <label className="registration-field">
                      <span>Approval exception ID (only with an approved exception)</span>
                      <input type="text" value={approvalWaiver()} onInput={(event: Event) => setApprovalWaiver(inputValue(event))} />
                    </label>
                    <Button variant="primary" type="submit" disabled={pending() || !canApprove}>
                      Approve commitment
                    </Button>
                  </Stack>
                </fieldset>
              </form>
            </>
          )}
          {error ? <p role="alert">{describeDecisionFailure(error)}</p> : null}
          {notice() ? <p role="status">{notice()}</p> : null}
        </Stack>
      </CardContent>
    </Card>
  );
}

function describeDecisionFailure(error: Error): string {
  if (error instanceof ProgramRequestError && error.status === 403) {
    return 'This decision was denied. Separation of duties blocks authors, the accepted reviewer (for approval), unassigned members when someone is assigned, and conflicting responsibilities unless an exact approved exception is supplied.';
  }
  if (error instanceof ProgramRequestError && error.status === 409 && !error.transient) {
    return `${error.message} The impact preview was reloaded; review it and acknowledge it again.`;
  }
  return describeCommitmentFailure(error);
}
