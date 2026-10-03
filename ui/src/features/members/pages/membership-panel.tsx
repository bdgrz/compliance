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

import {
  firmStaffNoAccessNotice,
  getMembership,
  listOpenResponsibilities,
  MemberRequestError,
  reinstateMember,
  suspendMember,
} from '../members.js';

const responsibilityLabels: Record<string, string> = {
  control_owner: 'Control owner',
  evidence_contributor: 'Evidence contributor',
  assigned_reviewer: 'Assigned reviewer',
  access_reviewer: 'Access reviewer',
  corrective_action_owner: 'Corrective action owner',
  policy_approver: 'Policy approver',
};

export function MembershipPanel({ userId, onChanged }: { userId: string; onChanged?: () => void }) {
  const [version, setVersion] = state(0);
  const [reason, setReason] = state('');
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<string | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  const membership = resource(() => getMembership(userId), [userId, version()]);
  const work = resource(() => listOpenResponsibilities(userId), [userId, version()]);

  async function run(action: () => Promise<void>, done: string) {
    setActionError(null);
    setNotice(null);
    setPending(true);
    try {
      await action();
      setNotice(done);
      setReason('');
      setVersion(version() + 1);
      onChanged?.();
    } catch (failure) {
      setActionError(failure instanceof Error ? failure.message : 'The change could not be saved.');
    } finally {
      setPending(false);
    }
  }

  function suspend(event?: { preventDefault?: () => void }) {
    event?.preventDefault?.();
    void run(() => suspendMember(userId, reason().trim()), 'Member suspended. Their access has ended.');
  }

  if (membership.pending) {
    return <Spinner label="Loading membership" />;
  }

  if (membership.error) {
    const forbidden = membership.error instanceof MemberRequestError && membership.error.status === 403;
    return (
      <Card>
        <CardContent>
          <Stack gap="sm">
            <p role="alert">{membership.error.message}</p>
            {forbidden ? null : (
              <Button variant="secondary" onPress={() => membership.refresh()}>
                Try again
              </Button>
            )}
          </Stack>
        </CardContent>
      </Card>
    );
  }

  const member = membership.value!;
  const openWork = work.value ?? [];

  return (
    <Stack gap="md">
      <Card>
        <CardHeader>
          <CardTitle>Membership</CardTitle>
          <CardDescription>
            {member.suspended
              ? `Suspended${member.suspendedBy ? ` by ${member.suspendedBy}` : ''}${
                  member.suspendedAt ? ` on ${new Date(member.suspendedAt).toLocaleDateString()}` : ''
                }${member.suspensionReason ? `: ${member.suspensionReason}` : ''}`
              : 'Active member of this organization.'}
          </CardDescription>
          {member.affiliation === 'firm_staff' ? (
            <CardDescription>Firm staff. {firmStaffNoAccessNotice}</CardDescription>
          ) : null}
        </CardHeader>
        <CardContent>
          <Stack gap="md">
            {actionError() ? <p role="alert">{actionError()}</p> : null}
            {notice() ? <p role="status">{notice()}</p> : null}
            {member.suspended ? (
              <Button
                variant="secondary"
                disabled={pending()}
                onPress={() => void run(() => reinstateMember(userId), 'Member reinstated.')}
              >
                {pending() ? 'Reinstating…' : 'Reinstate member'}
              </Button>
            ) : (
              <form onSubmit={(event) => suspend(event)}>
                <Stack gap="sm">
                  <p className="consequence">
                    Suspending ends all of this member's access to the organization immediately.
                    Their history is kept, and work assigned to them stays open until you reassign
                    it.
                  </p>
                  <label className="registration-field">
                    <span>Reason for suspension</span>
                    <textarea
                      value={reason()}
                      onInput={(event: Event) => setReason((event.target as HTMLTextAreaElement).value)}
                      required
                    />
                  </label>
                  <Button variant="destructive" type="submit" disabled={pending()}>
                    {pending() ? 'Suspending…' : 'Suspend member'}
                  </Button>
                </Stack>
              </form>
            )}
          </Stack>
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>{member.suspended ? 'Open work needing reassignment' : 'Open responsibilities'}</CardTitle>
          {member.suspended && openWork.length > 0 ? (
            <CardDescription>
              This member can no longer act on these. Assign each to an active member.
            </CardDescription>
          ) : null}
        </CardHeader>
        <CardContent>
          {work.pending ? (
            <Spinner label="Loading responsibilities" />
          ) : work.error ? (
            <Stack gap="sm">
              <p role="alert">{work.error.message}</p>
              <Button variant="secondary" onPress={() => work.refresh()}>
                Try again
              </Button>
            </Stack>
          ) : openWork.length === 0 ? (
            <p>No open responsibilities.</p>
          ) : (
            <ul className="plain-list">
              {openWork.map((item) => (
                <li>
                  <strong>{responsibilityLabels[item.type] ?? item.type}</strong> · {item.recordType}{' '}
                  {item.recordId.slice(0, 8)} (revision {item.revision}) · assigned{' '}
                  {new Date(item.assignedAt).toLocaleDateString()}
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </Stack>
  );
}
