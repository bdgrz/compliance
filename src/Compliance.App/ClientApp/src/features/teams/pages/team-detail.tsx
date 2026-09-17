import { state } from '@askrjs/askr';
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
  type TeamMemberSummary,
  type TeamSummary,
} from '../teams.js';

export function TeamDetailPage({ teamId }: { teamId: string }) {
  const [team, setTeam] = state<TeamSummary | null>(null);
  const [members, setMembers] = state<TeamMemberSummary[] | null>(null);
  const [memberId, setMemberId] = state('');
  const [error, setError] = state<string | null>(null);
  const [submitting, setSubmitting] = state(false);

  function load() {
    void getTeam(teamId)
      .then(setTeam)
      .catch((failure: unknown) =>
        setError(failure instanceof Error ? failure.message : 'Unable to load the team.')
      );
    void listTeamMembers(teamId)
      .then(setMembers)
      .catch((failure: unknown) =>
        setError(failure instanceof Error ? failure.message : 'Unable to load the team members.')
      );
  }

  load();

  async function add(event?: { preventDefault?: () => void }) {
    event?.preventDefault?.();
    setError(null);
    setSubmitting(true);
    try {
      await assignTeamMember(teamId, memberId());
      setMemberId('');
      load();
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
      load();
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
        <a href="/teams">Back to teams</a>
      </Button>
    </Page>
  );
}
