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
  assignRolePermission,
  assignTeamRole,
  getRole,
  listRolePermissions,
  listRoleTeams,
  removeRolePermission,
  removeTeamRole,
} from '../roles.js';
import { organizationPath } from '../../tenants/tenants.js';

export function RoleDetailPage({ roleId }: { roleId: string }) {
  const [permission, setPermission] = state('');
  const [teamId, setTeamId] = state('');
  const [actionError, setError] = state<string | null>(null);
  const [version, setVersion] = state(0);
  const [submittingPermission, setSubmittingPermission] = state(false);
  const [submittingTeam, setSubmittingTeam] = state(false);

  const roleResource = resource(() => getRole(roleId), [roleId, version()]);
  const role = () => (roleResource.pending ? null : roleResource.value);
  const permissionsResource = resource(() => listRolePermissions(roleId), [roleId, version()]);
  const permissions = () => (permissionsResource.pending ? null : permissionsResource.value);
  const teamsResource = resource(() => listRoleTeams(roleId), [roleId, version()]);
  const teams = () => (teamsResource.pending ? null : teamsResource.value);
  const error = () => actionError() ?? roleResource.error?.message ?? permissionsResource.error?.message ?? teamsResource.error?.message ?? null;

  function reload() {
    setVersion(version() + 1);
  }

  async function addPermission(event?: { preventDefault?: () => void }) {
    event?.preventDefault?.();
    setError(null);
    setSubmittingPermission(true);
    try {
      await assignRolePermission(roleId, permission());
      setPermission('');
      reload();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Unable to add that permission.');
    } finally {
      setSubmittingPermission(false);
    }
  }

  async function removePermission(targetPermission: string) {
    setError(null);
    try {
      await removeRolePermission(roleId, targetPermission);
      reload();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Unable to remove that permission.');
    }
  }

  async function addTeam(event?: { preventDefault?: () => void }) {
    event?.preventDefault?.();
    setError(null);
    setSubmittingTeam(true);
    try {
      await assignTeamRole(roleId, teamId());
      setTeamId('');
      reload();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Unable to grant that team this role.');
    } finally {
      setSubmittingTeam(false);
    }
  }

  async function removeTeam(targetTeamId: string) {
    setError(null);
    try {
      await removeTeamRole(roleId, targetTeamId);
      reload();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Unable to remove that team.');
    }
  }

  return (
    <Page>
      <PageHeader
        title={role()?.name ?? 'Role'}
        description="Teams are identified by team ID until a directory search exists."
      />
      {error() ? <p role="alert">{error()}</p> : null}
      <Card>
        <CardHeader>
          <CardTitle>Permissions</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={(event) => void addPermission(event)}>
            <Block direction="row" gap="md">
              <input
                type="text"
                placeholder="Permission, e.g. tenant.access"
                value={permission()}
                onInput={(event: Event) => setPermission((event.target as HTMLInputElement).value)}
                required
              />
              <Button variant="primary" type="submit" disabled={submittingPermission()}>
                {submittingPermission() ? 'Adding…' : 'Add permission'}
              </Button>
            </Block>
          </form>
          {permissions() === null ? (
            <p>Loading…</p>
          ) : (
            <Block direction="column" gap="sm">
              {(permissions() ?? []).map((item) => (
                <Block key={item.permission} direction="row" gap="md" align="center" justify="between">
                  <span>{item.permission}</span>
                  <Button variant="destructive" onPress={() => void removePermission(item.permission)}>
                    Remove
                  </Button>
                </Block>
              ))}
            </Block>
          )}
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>Teams</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={(event) => void addTeam(event)}>
            <Block direction="row" gap="md">
              <input
                type="text"
                placeholder="Team ID"
                value={teamId()}
                onInput={(event: Event) => setTeamId((event.target as HTMLInputElement).value)}
                required
              />
              <Button variant="primary" type="submit" disabled={submittingTeam()}>
                {submittingTeam() ? 'Granting…' : 'Grant to team'}
              </Button>
            </Block>
          </form>
          {teams() === null ? (
            <p>Loading…</p>
          ) : (
            <Block direction="column" gap="sm">
              {(teams() ?? []).map((item) => (
                <Block key={item.teamId} direction="row" gap="md" align="center" justify="between">
                  <span>{item.teamId}</span>
                  <Button variant="destructive" onPress={() => void removeTeam(item.teamId)}>
                    Remove
                  </Button>
                </Block>
              ))}
            </Block>
          )}
        </CardContent>
      </Card>
      <Button asChild variant="ghost">
        <a href={organizationPath('/roles')}>Back to roles</a>
      </Button>
    </Page>
  );
}
