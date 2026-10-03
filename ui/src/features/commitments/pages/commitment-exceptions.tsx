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

import { WaiverSummary } from '../../boundaries/pages/boundary-exceptions.js';
import {
  approveWaiver,
  getWaiver,
  listMemberOptions,
  recordWaiver,
  shortId,
  type ResponsibilityScope,
} from '../../boundaries/responsibilities.js';
import { organizationPath } from '../../tenants/tenants.js';
import { describeCommitmentFailure, getCommitmentDraft, kindTitle, ProgramRequestError } from '../commitments.js';

function defaultExpiry(): string {
  return new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
}

// A narrowly scoped, expiring exception for one member and one decision on the current revision.
export function CommitmentExceptionsPage({ programId, draftId }: { programId: string; draftId: string }) {
  const draft = resource(() => getCommitmentDraft(programId, draftId), [programId, draftId]);
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
  const detailPath = organizationPath(`/programs/${programId}/commitments/${draftId}`);
  const back = <a href={detailPath}>Back to commitment</a>;

  if (draft.pending && !draft.value) {
    return (
      <Page>
        <Spinner label="Loading commitment" />
      </Page>
    );
  }

  if (draft.error) {
    const error = draft.error;
    const status = error instanceof ProgramRequestError ? error.status : null;
    return (
      <Page>
        <EmptyState
          title={
            status === 403 ? 'Exceptions are not available to you' : status === 404 ? 'Commitment not found' : 'Commitment could not be loaded'
          }
          titleAs="h1"
          description={error.message}
          action={
            status === 403 || status === 404 ? (
              back
            ) : (
              <Button variant="primary" onPress={() => draft.refresh()}>
                Try again
              </Button>
            )
          }
        />
      </Page>
    );
  }

  const current = draft.value!;
  const scope: ResponsibilityScope | null =
    current.status === 'effective'
      ? null
      : { recordType: 'commitment', recordId: draftId, versionId: draftId, revision: current.revision };

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
        description={`Exceptions for ${kindTitle(current.kind)} ${current.identifier}, revision ${current.revision}.`}
      />
      <Stack gap="md">
        {back}
        {error ? <p role="alert">{describeCommitmentFailure(error, 'this exception')}</p> : null}
        {notice() ? <p role="status">{notice()}</p> : null}
        <Card>
          <CardHeader>
            <CardTitle>Record an exception</CardTitle>
            <CardDescription>
              {scope
                ? `Applies only to revision ${scope.revision}; a revised draft needs a new exception.`
                : 'This revision is already effective, so there is nothing to except.'}
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
                  <p role="alert">{describeCommitmentFailure(waiver.error, 'this exception')}</p>
                  <Button variant="secondary" onPress={() => waiver.refresh()}>
                    Try again
                  </Button>
                </Stack>
              ) : shown ? (
                <Stack gap="sm">
                  <WaiverSummary waiver={shown} />
                  <p>
                    Share this link with a second administrator:{' '}
                    <a href={`${organizationPath(`/programs/${programId}/commitments/${draftId}/exceptions`)}?exception=${shown.waiverId}`}>
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
