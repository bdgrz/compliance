import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
  Block,
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
import {
  builtInRoles,
  firmStaffNoAccessNotice,
  inviteMember,
  listInvitations,
  listMembers,
  memberLabel,
  MemberRequestError,
  roleLabel,
} from '../members.js';

export function MembersPage() {
  const [version, setVersion] = state(0);
  const [email, setEmail] = state('');
  const [role, setRole] = state(builtInRoles[0]!.value);
  const [submitting, setSubmitting] = state(false);
  const [actionError, setActionError] = state<string | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  const members = resource(() => listMembers(), [version()]);
  const invitations = resource(() => listInvitations(), [version()]);

  async function invite(event?: { preventDefault?: () => void }) {
    event?.preventDefault?.();
    setActionError(null);
    setNotice(null);
    setSubmitting(true);
    try {
      await inviteMember(email().trim(), role());
      setNotice(`Invitation sent to ${email().trim()} as ${roleLabel(role())}.`);
      setEmail('');
      setVersion(version() + 1);
    } catch (failure) {
      setActionError(failure instanceof Error ? failure.message : 'Unable to send the invitation.');
    } finally {
      setSubmitting(false);
    }
  }

  const loadError = members.error ?? invitations.error;
  if (loadError instanceof MemberRequestError && loadError.status === 403) {
    return (
      <Page>
        <EmptyState
          title="Members are not available to you"
          titleAs="h1"
          description="Only organization administrators can manage members. Ask an Org Admin if you need access."
        />
      </Page>
    );
  }

  const pending = (invitations.value ?? []).filter((invitation) => invitation.status === 'pending');

  return (
    <Page>
      <PageHeader title="Members" description="Invite people and see how each member gets access." />
      <Card>
        <CardHeader>
          <CardTitle>Invite a member</CardTitle>
          <CardDescription>
            The invitee proves their email and accepts the invitation themselves. Their role takes
            effect when they accept.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={(event) => void invite(event)}>
            <Stack gap="md">
              <label className="registration-field">
                <span>Email address</span>
                <input
                  type="email"
                  autocomplete="off"
                  value={email()}
                  onInput={(event: Event) => setEmail((event.target as HTMLInputElement).value)}
                  required
                />
              </label>
              <fieldset className="role-choice">
                <legend>Role</legend>
                {builtInRoles.map((option) => (
                  <label className="role-option">
                    <input
                      type="radio"
                      name="built_in_role"
                      value={option.value}
                      checked={role() === option.value}
                      onChange={() => setRole(option.value)}
                    />
                    <span>
                      <strong>{option.label}</strong>
                      <span className="role-explanation">{option.explanation}</span>
                    </span>
                  </label>
                ))}
              </fieldset>
              {actionError() ? <p role="alert">{actionError()}</p> : null}
              {notice() ? <p role="status">{notice()}</p> : null}
              <Button variant="primary" type="submit" disabled={submitting()}>
                {submitting() ? 'Sending…' : 'Send invitation'}
              </Button>
            </Stack>
          </form>
        </CardContent>
      </Card>

      {loadError ? (
        <Card>
          <CardContent>
            <Stack gap="sm">
              <p role="alert">{loadError.message}</p>
              <Button
                variant="secondary"
                onPress={() => {
                  members.refresh();
                  invitations.refresh();
                }}
              >
                Try again
              </Button>
            </Stack>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Pending invitations</CardTitle>
        </CardHeader>
        <CardContent>
          {invitations.pending ? (
            <Spinner label="Loading invitations" />
          ) : pending.length === 0 ? (
            <p>No pending invitations.</p>
          ) : (
            <ul className="plain-list">
              {pending.map((invitation) => (
                <li>
                  <strong>{invitation.emailAddress}</strong> ·{' '}
                  {invitation.administrator ? 'Org Admin' : roleLabel(invitation.builtInRole)} · expires{' '}
                  {new Date(invitation.expiresAt).toLocaleDateString()}
                  {invitation.deliveryStatus && invitation.deliveryStatus !== 'delivered'
                    ? ` · delivery ${invitation.deliveryStatus}`
                    : ''}
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Active members</CardTitle>
        </CardHeader>
        <CardContent>
          {(members.value ?? []).some((member) => member.affiliation === 'firm_staff') ? (
            <p className="consequence">
              Firm staff are added by a platform operator. Engagement assignments are accepted
              separately and are what give firm staff a role on business records.
            </p>
          ) : null}
          {members.pending ? (
            <Spinner label="Loading members" />
          ) : (members.value ?? []).length === 0 ? (
            <p>No members yet.</p>
          ) : (
            <ul className="plain-list">
              {(members.value ?? []).map((member) => (
                <li>
                  <Block direction="row" gap="sm" align="center" wrap>
                    <a href={organizationPath(`/members/${member.userId}`)}>
                      {memberLabel(member, member.userId)}
                    </a>
                    {member.emailAddress && member.emailAddress !== memberLabel(member, member.userId)
                      ? <span>{member.emailAddress}</span> : null}
                    <span>{member.affiliation === 'firm_staff' ? 'Firm staff' : 'Client personnel'}</span>
                    <span>{member.suspended ? 'Suspended' : 'Active'}</span>
                  </Block>
                  {member.affiliation === 'firm_staff' ? (
                    <p className="role-explanation">{firmStaffNoAccessNotice}</p>
                  ) : null}
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </Page>
  );
}
