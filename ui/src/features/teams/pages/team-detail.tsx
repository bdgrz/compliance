import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
  Block,
  Button,
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  Page,
  PageHeader,
  EmptyState,
  Spinner,
} from '@askrjs/themes/components';

import {
  assignTeamMember,
  getTeam,
  listTeamMembers,
  removeTeamMember,
  TeamRequestError,
} from '../teams.js';
import { organizationPath } from '../../tenants/tenants.js';
import { TeamRolesPanel } from './team-roles-panel.js';

export function TeamDetailPage({ teamId }: { teamId: string }) {
  const [memberId, setMemberId] = state('');
  const [actionError, setError] = state<string | null>(null);
  const [version, setVersion] = state(0);
  const [submitting, setSubmitting] = state(false);

  const teamResource = resource(() => getTeam(teamId), [teamId, version()]);
  const team = () => (teamResource.pending ? null : teamResource.value);
  const membersResource = resource(() => listTeamMembers(teamId), [teamId, version()]);
  const members = () => (membersResource.pending ? null : membersResource.value);
  const error = () => actionError() ?? teamResource.error?.message ?? membersResource.error?.message ?? null;

  function reload() {
    setVersion(version() + 1);
  }

  async function add(event?: { preventDefault?: () => void }) {
    event?.preventDefault?.();
    setError(null);
    setSubmitting(true);
    try {
      await assignTeamMember(teamId, memberId());
      setMemberId('');
      reload();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Unable to add that member.');
    } finally {
      setSubmitting(false);
    }
  }

  async function remove(targetMemberId: string) {
    setError(null);
    try {
      await removeTeamMember(teamId, targetMemberId);
      reload();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Unable to remove that member.');
    }
  }

  if (teamResource.pending || membersResource.pending) {
    return <Page><Spinner label="Loading team members" /></Page>;
  }
  const loadError = teamResource.error ?? membersResource.error;
  if (loadError || !team()) {
    const status = loadError instanceof TeamRequestError ? loadError.status : null;
    return (
      <Page>
        <EmptyState
          title={status === 403 ? 'Team members are not available to you'
            : status === 404 || !team() && !loadError ? 'Team not found' : 'Team members could not be loaded'}
          titleAs="h1"
          description={loadError?.message ?? 'This team could not be found.'}
          action={status === 403 || status === 404 || !loadError
            ? <a href={organizationPath('/teams')}>Back to teams</a>
            : <Button onPress={reload}>Try again</Button>}
        />
      </Page>
    );
  }

  return (
    <Page>
      <PageHeader
        title={team()?.name ?? 'Team'}
        description="Manage who belongs to this team."
      />
      <TeamRolesPanel teamId={teamId} />
      <Card>
        <CardHeader>
          <CardTitle>Add a member</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={(event) => void add(event)}>
            <Block direction="row" gap="md">
              <label className="registration-field">
                <span>Member ID</span>
                <input
                type="text"
                placeholder="Member ID"
                value={memberId()}
                onInput={(event: Event) => setMemberId((event.target as HTMLInputElement).value)}
                required
                />
              </label>
              <Button variant="primary" type="submit" disabled={submitting()}>
                {submitting() ? 'Adding…' : 'Add member'}
              </Button>
            </Block>
          </form>
        </CardContent>
      </Card>
      {error() ? <p role="alert">{error()}</p> : null}
      {members() === null && !error() ? <p>Loading…</p> : null}
      {members()?.length === 0 ? <p>No members belong to this team yet.</p> : null}
      <Block direction="column" gap="md">
        {(members() ?? []).map((member) => (
          <Card key={member.memberId}>
            <CardContent>
              <Block direction="row" gap="md" align="center" justify="between">
                <Block direction="column" gap="sm">
                  {member.userId ? <a href={organizationPath(`/members/${member.userId}`)}>
                    {member.displayName ?? member.emailAddress ?? member.memberId}
                  </a> : <span>{member.displayName ?? member.emailAddress ?? member.memberId}</span>}
                  {member.emailAddress && member.emailAddress !== member.displayName
                    ? <span>{member.emailAddress}</span> : null}
                </Block>
                <Button
                  variant="destructive"
                  aria-label={`Remove ${member.displayName ?? member.emailAddress ?? member.memberId} from this team`}
                  onPress={() => void remove(member.memberId)}
                >
                  Remove
                </Button>
              </Block>
            </CardContent>
          </Card>
        ))}
      </Block>
      <Button asChild variant="ghost">
        <a href={organizationPath('/teams')}>Back to teams</a>
      </Button>
    </Page>
  );
}
