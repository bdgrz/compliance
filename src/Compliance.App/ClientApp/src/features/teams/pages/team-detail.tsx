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
} from '@askrjs/themes/components';

import {
  assignTeamMember,
  getTeam,
  listTeamMembers,
  removeTeamMember,
} from '../teams.js';
import { organizationPath } from '../../tenants/tenants.js';

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

  return (
    <Page>
      <PageHeader
        title={team()?.name ?? 'Team'}
        description="Members are identified by member ID until a directory search exists."
      />
      <Card>
        <CardHeader>
          <CardTitle>Add a member</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={(event) => void add(event)}>
            <Block direction="row" gap="md">
              <input
                type="text"
                placeholder="Member ID"
                value={memberId()}
                onInput={(event: Event) => setMemberId((event.target as HTMLInputElement).value)}
                required
              />
              <Button variant="primary" type="submit" disabled={submitting()}>
                {submitting() ? 'Adding…' : 'Add member'}
              </Button>
            </Block>
          </form>
        </CardContent>
      </Card>
      {error() ? <p role="alert">{error()}</p> : null}
      {members() === null && !error() ? <p>Loading…</p> : null}
      <Block direction="column" gap="md">
        {(members() ?? []).map((member) => (
          <Card key={member.memberId}>
            <CardContent>
              <Block direction="row" gap="md" align="center" justify="between">
                <span>{member.memberId}</span>
                <Button variant="destructive" onPress={() => void remove(member.memberId)}>
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
