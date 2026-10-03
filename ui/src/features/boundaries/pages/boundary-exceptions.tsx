import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  EmptyState,
  Page,
  PageHeader,
  Spinner,
  Stack,
} from '@askrjs/themes/components';

import { organizationPath } from '../../tenants/tenants.js';
import { getBoundary, label, ProgramRequestError } from '../boundaries.js';
import { describeBoundaryFailure } from '../components/boundary-form.js';
import {
  approveWaiver,
  getWaiver,
  listMemberOptions,
  recordWaiver,
  shortId,
  type ResponsibilityScope,
  type Waiver,
} from '../responsibilities.js';

function defaultExpiry(): string {
  const date = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000);
  return date.toISOString().slice(0, 10);
}

// Actor, rationale, scope, expiry, and the second administrator's approval, stated in full.
export function WaiverSummary({ waiver }: { waiver: Waiver }) {
  const expired = new Date(waiver.expiresAt).getTime() <= Date.now();
  return (
    <dl className="boundary-waiver">
      <dt>Status</dt>
      <dd>
        {label(waiver.status)}
        {waiver.active ? ' · active' : expired ? ' · expired' : ' · not active'}
      </dd>
      <dt>Requested by</dt>
      <dd>
        {waiver.requester} on {new Date(waiver.requestedAt).toLocaleString()}
      </dd>
      <dt>Rationale</dt>
      <dd>{waiver.rationale}</dd>
      <dt>Scope</dt>
      <dd>
        Lets member {shortId(waiver.beneficiaryMemberId)} {waiver.scope.action} {label(waiver.scope.recordType)} revision{' '}
        {waiver.scope.revision} (version {shortId(waiver.scope.versionId)}) only
      </dd>
      <dt>Expires</dt>
      <dd>{new Date(waiver.expiresAt).toLocaleString()}</dd>
      <dt>Second-administrator approval</dt>
      <dd>
        {waiver.approver && waiver.approvedAt
          ? `${waiver.approver} on ${new Date(waiver.approvedAt).toLocaleString()}`
          : 'Awaiting approval by a different administrator'}
      </dd>
      <dt>Exception ID</dt>
      <dd>
        <code>{waiver.waiverId}</code>
      </dd>
    </dl>
  );
}

export function BoundaryExceptionsPage({ programId, boundaryId }: { programId: string; boundaryId: string }) {
  const boundary = resource(() => getBoundary(boundaryId), [boundaryId]);
  const members = resource(() => listMemberOptions(), []);
  const initialId = new URLSearchParams(window.location.search).get('exception') ?? '';
  const [lookupId, setLookupId] = state(initialId);
  const [shownId, setShownId] = state(initialId);
  const [waiverVersion, setWaiverVersion] = state(0);
  const waiver = resource(() => (shownId() ? getWaiver(shownId()) : Promise.resolve(null)), [shownId(), waiverVersion()]);
  const [action, setAction] = state('review');
  const [beneficiary, setBeneficiary] = state('');
  const [rationale, setRationale] = state('');
  const [expires, setExpires] = state(defaultExpiry());
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);
  const boundaryPath = organizationPath(`/programs/${programId}/boundaries/${boundaryId}`);
  const back = <a href={boundaryPath}>Back to boundary</a>;

  if (boundary.pending && !boundary.value) {
    return (
      <Page>
        <Spinner label="Loading boundary" />
      </Page>
    );
  }

  if (boundary.error) {
    const error = boundary.error;
    const status = error instanceof ProgramRequestError ? error.status : null;
    return (
      <Page>
        <EmptyState
          title={status === 403 ? 'Exceptions are not available to you' : status === 404 ? 'Boundary not found' : 'Boundary could not be loaded'}
          titleAs="h1"
          description={error.message}
          action={
            status === 403 || status === 404 ? (
              back
            ) : (
              <Button variant="primary" onPress={() => boundary.refresh()}>
                Try again
              </Button>
            )
          }
        />
      </Page>
    );
  }

  const draft = boundary.value!.draft;
  const scope: ResponsibilityScope | null = draft
    ? { recordType: 'boundary', recordId: boundaryId, versionId: draft.versionId, revision: draft.revision }
    : null;

  async function run(work: () => Promise<string>) {
    setActionError(null);
    setNotice(null);
    setPending(true);
    try {
      setNotice(await work());
    } catch (failure) {
      setActionError(failure instanceof Error ? failure : new Error('The request failed.'));
    } finally {
      setPending(false);
    }
  }

  function record(event: Event) {
    event.preventDefault();
    if (!scope) return;
    void run(async () => {
      const recorded = await recordWaiver(scope, action(), beneficiary(), rationale(), new Date(`${expires()}T23:59:59Z`).toISOString());
      setLookupId(recorded.waiverId);
      setShownId(recorded.waiverId);
      setWaiverVersion(waiverVersion() + 1);
      setRationale('');
      return 'Exception recorded. A different administrator must approve it before it can be used.';
    });
  }

  function approve(waiverId: string) {
    void run(async () => {
      await approveWaiver(waiverId);
      setWaiverVersion(waiverVersion() + 1);
      return 'Exception approved.';
    });
  }

  const error = actionError();
  const shown = waiver.value;

  return (
    <Page>
      <PageHeader
        title="Separation-of-duties exceptions"
        description="Record a narrowly scoped, expiring exception for one member and one decision on this boundary's current draft revision."
      />
      <Stack gap="md">
        {back}
        {error ? <p role="alert">{describeBoundaryFailure(error, 'this exception')}</p> : null}
        {notice() ? <p role="status">{notice()}</p> : null}
        <Card>
          <CardHeader>
            <CardTitle>Record an exception</CardTitle>
            <CardDescription>
              {scope
                ? `Applies only to draft revision ${scope.revision}; a revised draft needs a new exception.`
                : 'This boundary has no open draft, so there is nothing to except.'}
            </CardDescription>
          </CardHeader>
          <CardContent>
            {scope ? (
              <form onSubmit={(event: Event) => record(event)}>
                <Stack gap="sm">
                  <label className="registration-field">
                    <span>Member who needs the exception</span>
                    <select value={beneficiary()} onChange={(event: Event) => setBeneficiary((event.target as HTMLSelectElement).value)} required>
                      <option value="" selected={beneficiary() === ''}>
                        Choose a member
                      </option>
                      {(members.value ?? []).map((member) => (
                        <option value={member.userId} selected={member.userId === beneficiary()}>
                          {member.label}
                        </option>
                      ))}
                    </select>
                  </label>
                  {members.error ? <p role="alert">{members.error.message}</p> : null}
                  <label className="registration-field">
                    <span>Decision allowed</span>
                    <select value={action()} onChange={(event: Event) => setAction((event.target as HTMLSelectElement).value)}>
                      <option value="review" selected={action() === 'review'}>
                        Review
                      </option>
                      <option value="approve" selected={action() === 'approve'}>
                        Approve
                      </option>
                    </select>
                  </label>
                  <label className="registration-field">
                    <span>Rationale</span>
                    <textarea rows={3} value={rationale()} onInput={(event: Event) => setRationale((event.target as HTMLTextAreaElement).value)} required />
                  </label>
                  <label className="registration-field">
                    <span>Expires at the end of</span>
                    <input type="date" value={expires()} onInput={(event: Event) => setExpires((event.target as HTMLInputElement).value)} required />
                  </label>
                  <Button variant="primary" type="submit" disabled={pending()}>
                    Record exception
                  </Button>
                </Stack>
              </form>
            ) : null}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Review an exception</CardTitle>
            <CardDescription>Open an exception by ID to see who requested it and to approve it as a second administrator.</CardDescription>
          </CardHeader>
          <CardContent>
            <Stack gap="sm">
              <form
                onSubmit={(event: Event) => {
                  event.preventDefault();
                  setShownId(lookupId().trim());
                }}
              >
                <Stack gap="sm">
                  <label className="registration-field">
                    <span>Exception ID</span>
                    <input type="text" value={lookupId()} onInput={(event: Event) => setLookupId((event.target as HTMLInputElement).value)} required />
                  </label>
                  <Button variant="secondary" type="submit">
                    Open exception
                  </Button>
                </Stack>
              </form>
              {!shownId() ? null : waiver.pending && !shown ? (
                <Spinner label="Loading exception" />
              ) : waiver.error ? (
                <Stack gap="sm">
                  <p role="alert">{describeBoundaryFailure(waiver.error, 'this exception')}</p>
                  <Button variant="secondary" onPress={() => waiver.refresh()}>
                    Try again
                  </Button>
                </Stack>
              ) : shown ? (
                <Stack gap="sm">
                  <WaiverSummary waiver={shown} />
                  <p>
                    Share this link with a second administrator:{' '}
                    <a href={`${organizationPath(`/programs/${programId}/boundaries/${boundaryId}/exceptions`)}?exception=${shown.waiverId}`}>
                      exception {shortId(shown.waiverId)}
                    </a>
                  </p>
                  {shown.approvedAt ? null : (
                    <Button variant="primary" disabled={pending()} onPress={() => approve(shown.waiverId)}>
                      Approve as second administrator
                    </Button>
                  )}
                </Stack>
              ) : null}
            </Stack>
          </CardContent>
        </Card>
      </Stack>
    </Page>
  );
}
