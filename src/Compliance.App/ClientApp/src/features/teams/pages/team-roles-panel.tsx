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

import { assignTeamRole, listRoles, listTeamRoles, removeTeamRole } from '../../roles/roles.js';

export function TeamRolesPanel({ teamId }: { teamId: string }) {
  const [version, setVersion] = state(0);
  const [selected, setSelected] = state('');
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<string | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  const granted = resource(() => listTeamRoles(teamId), [teamId, version()]);
  const available = resource(() => listRoles(), [version()]);

  async function change(action: () => Promise<void>, done: string) {
    setActionError(null);
    setNotice(null);
    setPending(true);
    try {
      await action();
      setNotice(done);
      setSelected('');
      setVersion(version() + 1);
    } catch (failure) {
      setActionError(failure instanceof Error ? failure.message : 'The change could not be saved.');
    } finally {
      setPending(false);
    }
  }

  const grantedIds = new Set((granted.value ?? []).map((role) => role.roleId));
  const grantable = (available.value ?? []).filter((role) => !grantedIds.has(role.roleId));

  return (
    <Card>
      <CardHeader>
        <CardTitle>Roles granted to this team</CardTitle>
        <CardDescription>
          Every current member of this team gets the permissions of each role below. Removing a role,
          or removing someone from the team, takes that access away immediately unless another team
          or access grant still provides it.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Stack gap="md">
          {actionError() ? <p role="alert">{actionError()}</p> : null}
          {notice() ? <p role="status">{notice()}</p> : null}
          {granted.pending ? (
            <Spinner label="Loading team roles" />
          ) : granted.error ? (
            <Stack gap="sm">
              <p role="alert">{granted.error.message}</p>
              <Button variant="secondary" onPress={() => granted.refresh()}>
                Try again
              </Button>
            </Stack>
          ) : (granted.value ?? []).length === 0 ? (
            <p>No roles are granted to this team, so membership gives no access.</p>
          ) : (
            <ul className="plain-list">
              {(granted.value ?? []).map((role) => (
                <li className="team-role">
                  <span>{role.name}</span>
                  <Button
                    variant="destructive"
                    size="sm"
                    disabled={pending()}
                    aria-label={`Remove role ${role.name} from this team`}
                    onPress={() =>
                      void change(
                        () => removeTeamRole(role.roleId, teamId),
                        `Removed ${role.name}. Team members lost the access it provided.`
                      )
                    }
                  >
                    Remove
                  </Button>
                </li>
              ))}
            </ul>
          )}
          {grantable.length > 0 ? (
            <form
              onSubmit={(event: Event) => {
                event.preventDefault();
                const role = grantable.find((r) => r.roleId === selected());
                if (role) {
                  void change(() => assignTeamRole(role.roleId, teamId), `Granted ${role.name} to this team.`);
                }
              }}
            >
              <Stack gap="sm">
                <label className="registration-field">
                  <span>Grant a role</span>
                  <select
                    value={selected()}
                    onChange={(event: Event) => setSelected((event.target as HTMLSelectElement).value)}
                    required
                  >
                    <option value="">Choose a role</option>
                    {grantable.map((role) => (
                      <option value={role.roleId}>{role.name}</option>
                    ))}
                  </select>
                </label>
                <Button variant="primary" type="submit" disabled={pending()}>
                  {pending() ? 'Saving…' : 'Grant role'}
                </Button>
              </Stack>
            </form>
          ) : null}
        </Stack>
      </CardContent>
    </Card>
  );
}
