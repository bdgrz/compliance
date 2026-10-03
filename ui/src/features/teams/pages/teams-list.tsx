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

import { defineTeam, deleteTeam, listTeams } from '../teams.js';
import { organizationPath } from '../../tenants/tenants.js';

export function TeamsListPage() {
  const [name, setName] = state('');
  const [actionError, setError] = state<string | null>(null);
  const [version, setVersion] = state(0);
  const [submitting, setSubmitting] = state(false);

  const teamsResource = resource(() => listTeams(), [version()]);
  const teams = () => (teamsResource.pending ? null : teamsResource.value);
  const error = () => actionError() ?? teamsResource.error?.message ?? null;

  function reload() {
    setVersion(version() + 1);
  }

  async function create(event?: { preventDefault?: () => void }) {
    event?.preventDefault?.();
    setError(null);
    setSubmitting(true);
    try {
      await defineTeam(name());
      setName('');
      reload();
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
      reload();
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
                  <a href={organizationPath(`/teams/${team.teamId}`)}>{team.name}</a>
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
