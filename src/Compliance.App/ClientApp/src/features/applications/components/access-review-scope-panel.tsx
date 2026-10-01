import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import { Button, Card, CardContent, CardDescription, CardHeader, CardTitle, Spinner, Stack } from '@askrjs/themes/components';

import {
  ApplicationRequestError,
  decideAccessReviewScope,
  getAccessReviewScope,
  listInstanceBoundaryReferences,
  retireSystemInstance,
  type InstanceBoundaryReference,
  type SystemInstance,
} from '../applications.js';
import { organizationPath } from '../../tenants/tenants.js';
import { messageFor, startOfDay, today } from './messages.js';

const statusLabels: Record<string, string> = {
  included: 'Included in access reviews',
  excluded: 'Excluded from access reviews',
  unresolved: 'Unresolved: no scope decision applies',
};

export function AccessReviewScopePanel({
  applicationId,
  instance,
  onChanged,
}: {
  applicationId: string;
  instance: SystemInstance;
  onChanged: () => void;
}) {
  const [version, setVersion] = state(0);
  const [asOf, setAsOf] = state('');
  const scope = resource(
    () => getAccessReviewScope(applicationId, instance.systemInstanceId, asOf() ? startOfDay(asOf()) : null),
    [applicationId, instance.systemInstanceId, asOf(), version()]
  );
  const [decision, setDecision] = state<'included' | 'excluded'>('included');
  const [reason, setReason] = state('');
  const [effectiveFrom, setEffectiveFrom] = state(today());
  const [reviewBy, setReviewBy] = state('');
  const [waiverId, setWaiverId] = state('');
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);
  const [retireReason, setRetireReason] = state('');
  const [retireAt, setRetireAt] = state(today());
  const [retireError, setRetireError] = state<Error | null>(null);
  const [retirePending, setRetirePending] = state(false);
  const [impact, setImpact] = state<InstanceBoundaryReference[] | null>(null);
  const retired = instance.lifecycle === 'retired';

  async function decide(event: Event) {
    event.preventDefault();
    const current = scope.value;
    if (!current) return;
    setActionError(null);
    setNotice(null);
    setPending(true);
    try {
      await decideAccessReviewScope(applicationId, instance.systemInstanceId, {
        expectedSystemInstanceRevision: instance.revision,
        expectedDecisionCount: current.decisions.length,
        decision: decision(),
        reason: reason().trim(),
        effectiveFrom: startOfDay(effectiveFrom()),
        reviewBy: reviewBy() ? startOfDay(reviewBy()) : null,
        waiverId: waiverId().trim() || null,
      });
      setNotice('Scope decision recorded.');
      setReason('');
      setVersion(version() + 1);
    } catch (failure) {
      setActionError(failure instanceof Error ? failure : new Error('Unable to record the scope decision.'));
    } finally {
      setPending(false);
    }
  }

  // Step one: show which boundary entries still name this instance before anything changes.
  async function retire(event: Event) {
    event.preventDefault();
    setRetireError(null);
    setImpact(null);
    setRetirePending(true);
    try {
      setImpact(await listInstanceBoundaryReferences(applicationId, instance.systemInstanceId));
    } catch (failure) {
      setRetireError(failure instanceof Error ? failure : new Error('Unable to preview the retirement.'));
    } finally {
      setRetirePending(false);
    }
  }

  async function confirmRetire() {
    setRetireError(null);
    setRetirePending(true);
    try {
      await retireSystemInstance(applicationId, instance, startOfDay(retireAt()), retireReason().trim());
      onChanged();
    } catch (failure) {
      setRetireError(failure instanceof Error ? failure : new Error('Unable to retire the system instance.'));
    } finally {
      setRetirePending(false);
    }
  }

  const error = actionError();
  const current = scope.value;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Access-review scope: {instance.name}</CardTitle>
        <CardDescription>
          Scope decisions are personal approvals recorded in this app only. The member who registered a system
          instance cannot decide its scope without a separation-of-duties waiver.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Stack gap="sm">
          <label className="registration-field">
            <span>Show decision as of</span>
            <input type="date" value={asOf()} onInput={(event: Event) => setAsOf((event.target as HTMLInputElement).value)} />
          </label>
          {scope.pending && !current ? (
            <Spinner label="Loading access-review scope" />
          ) : scope.error ? (
            <Stack gap="sm">
              <p role="alert">
                {scope.error instanceof ApplicationRequestError && scope.error.status === 403
                  ? 'You do not have permission to view this access-review scope.'
                  : scope.error.message}
              </p>
              <Button variant="secondary" onPress={() => scope.refresh()}>
                Try again
              </Button>
            </Stack>
          ) : current ? (
            <>
              <p className="scope-status">
                <strong>{statusLabels[current.status] ?? current.status}</strong> as of{' '}
                {new Date(current.asOf).toLocaleString()}
                {current.effective ? ` · ${current.effective.reason} (approved by ${current.effective.approvedBy})` : ''}
              </p>
              <h3>Decision history</h3>
              {current.decisions.length === 0 ? (
                <p>No scope decisions yet.</p>
              ) : (
                <ol className="plain-list scope-history">
                  {current.decisions.map((entry) => (
                    <li>
                      <strong>{entry.decision === 'included' ? 'Included' : 'Excluded'}</strong> from{' '}
                      {new Date(entry.effectiveFrom).toLocaleDateString()} by {entry.approvedBy}: {entry.reason}
                      {entry.reviewBy ? ` · review by ${new Date(entry.reviewBy).toLocaleDateString()}` : ''}
                      {entry.waiverId ? ' · under a separation-of-duties waiver' : ''}
                    </li>
                  ))}
                </ol>
              )}
              {retired ? (
                <p>This system instance is retired; its scope history is kept and no new decisions can be made.</p>
              ) : (
                <form onSubmit={(event: Event) => void decide(event)} aria-label="Decide access-review scope">
                  <Stack gap="sm">
                    <fieldset className="scope-choice">
                      <legend>Decision</legend>
                      <label>
                        <input
                          type="radio"
                          name={`scope-${instance.systemInstanceId}`}
                          checked={decision() === 'included'}
                          onChange={() => setDecision('included')}
                        />{' '}
                        Include in access reviews
                      </label>
                      <label>
                        <input
                          type="radio"
                          name={`scope-${instance.systemInstanceId}`}
                          checked={decision() === 'excluded'}
                          onChange={() => setDecision('excluded')}
                        />{' '}
                        Exclude from access reviews
                      </label>
                    </fieldset>
                    <label className="registration-field">
                      <span>Rationale</span>
                      <input
                        type="text"
                        value={reason()}
                        onInput={(event: Event) => setReason((event.target as HTMLInputElement).value)}
                        required
                      />
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
                      <span>Review by (optional)</span>
                      <input type="date" value={reviewBy()} onInput={(event: Event) => setReviewBy((event.target as HTMLInputElement).value)} />
                    </label>
                    <label className="registration-field">
                      <span>Separation-of-duties waiver ID (optional)</span>
                      <input type="text" value={waiverId()} onInput={(event: Event) => setWaiverId((event.target as HTMLInputElement).value)} />
                    </label>
                    {error ? <p role="alert">{messageFor(error, 'scope decisions for this system instance')}</p> : null}
                    {notice() ? <p role="status">{notice()}</p> : null}
                    <Button variant="primary" type="submit" disabled={pending()}>
                      {pending() ? 'Recording…' : 'Record scope decision'}
                    </Button>
                  </Stack>
                </form>
              )}
            </>
          ) : null}
          {retired ? null : (
            <form onSubmit={(event: Event) => void retire(event)} aria-label="Retire system instance">
              <Stack gap="sm">
                <h3>Retire this system instance</h3>
                <label className="registration-field">
                  <span>Retirement reason</span>
                  <input
                    type="text"
                    value={retireReason()}
                    onInput={(event: Event) => setRetireReason((event.target as HTMLInputElement).value)}
                    required
                  />
                </label>
                <label className="registration-field">
                  <span>Retirement effective date</span>
                  <input
                    type="date"
                    value={retireAt()}
                    onInput={(event: Event) => setRetireAt((event.target as HTMLInputElement).value)}
                    required
                  />
                </label>
                {retireError() ? <p role="alert">{messageFor(retireError()!, 'this system instance')}</p> : null}
                <Button variant="secondary" type="submit" disabled={retirePending()}>
                  {retirePending() && !impact() ? 'Previewing…' : 'Preview retirement'}
                </Button>
              </Stack>
            </form>
          )}
          {!retired && impact() ? (
            <Stack gap="sm">
              <h4>Retirement impact</h4>
              {impact()!.length === 0 ? (
                <p role="status">No boundary entries reference this system instance.</p>
              ) : (
                <>
                  <p role="status">
                    {impact()!.length} boundary {impact()!.length === 1 ? 'entry still names' : 'entries still name'} this
                    system instance. They stay as recorded; review them after retiring.
                  </p>
                  <ul className="plain-list application-impact">
                    {impact()!.map((reference) => (
                      <li>
                        <a href={organizationPath(`/programs/${reference.programId}/boundaries/${reference.boundaryId}`)}>
                          {reference.subject}
                        </a>{' '}
                        · {reference.kind.replaceAll('_', ' ')} · {reference.status.replaceAll('_', ' ')}
                        {reference.rationale ? ` · ${reference.rationale}` : ''}
                      </li>
                    ))}
                  </ul>
                </>
              )}
              <Button variant="destructive" onPress={() => void confirmRetire()} disabled={retirePending()}>
                {retirePending() ? 'Retiring…' : 'Retire system instance'}
              </Button>
            </Stack>
          ) : null}
        </Stack>
      </CardContent>
    </Card>
  );
}
