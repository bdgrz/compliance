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

import { defineTeam, deleteTeam, listTeams, type TeamSummary } from '../teams.js';

export function TeamsListPage() {
  const [teams, setTeams] = state<TeamSummary[] | null>(null);
  const [name, setName] = state('');
  const [error, setError] = state<string | null>(null);
  const [submitting, setSubmitting] = state(false);

  function load() {
    void listTeams()
      .then(setTeams)
      .catch((failure: unknown) =>
        setError(failure instanceof Error ? failure.message : 'Unable to load teams.')
      );
  }

  load();

  async function create(event?: { preventDefault?: () => void }) {
    event?.preventDefault?.();
    setError(null);
    setSubmitting(true);
    try {
      await defineTeam(name());
      setName('');
      load();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Unable to create the team.');
    } finally {
      setSubmitting(false);
    }
  }

  async function remove(teamId: string) {
    setError(null);
    try {
      await deleteTeam(teamId);
      load();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Unable to delete the team.');
    }
  }

  return (
    <Page>
      <PageHeader title="Teams" description="Create teams and manage who belongs to each one." />
      <Card>
        <CardHeader>
          <CardTitle>New team</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={(event) => void create(event)}>
            <Block direction="row" gap="md">
              <input
                type="text"
                placeholder="Team name"
                value={name()}
                onInput={(event: Event) => setName((event.target as HTMLInputElement).value)}
                required
              />
              <Button variant="primary" type="submit" disabled={submitting()}>
                {submitting() ? 'Creating…' : 'Create team'}
              </Button>
            </Block>
          </form>
        </CardContent>
      </Card>
      {error() ? <p role="alert">{error()}</p> : null}
      {teams() === null && !error() ? <p>Loading…</p> : null}
      <Block direction="column" gap="md">
        {(teams() ?? []).map((team) => (
          <Card key={team.teamId}>
            <CardContent>
              <Block direction="row" gap="md" align="center" justify="between">
                <Button asChild variant="ghost">
                  <a href={`/teams/${team.teamId}`}>{team.name}</a>
                </Button>
                <Button variant="destructive" onPress={() => void remove(team.teamId)}>
                  Delete
                </Button>
              </Block>
            </CardContent>
          </Card>
        ))}
      </Block>
    </Page>
  );
}
