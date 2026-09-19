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

import { defineRole, deleteRole, listRoles, type RoleSummary } from '../roles.js';

export function RolesListPage() {
  const [roles, setRoles] = state<RoleSummary[] | null>(null);
  const [name, setName] = state('');
  const [error, setError] = state<string | null>(null);
  const [submitting, setSubmitting] = state(false);

  function load() {
    void listRoles()
      .then(setRoles)
      .catch((failure: unknown) =>
        setError(failure instanceof Error ? failure.message : 'Unable to load roles.')
      );
  }

  load();

  async function create(event?: { preventDefault?: () => void }) {
    event?.preventDefault?.();
    setError(null);
    setSubmitting(true);
    try {
      await defineRole(name());
      setName('');
      load();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Unable to create the role.');
    } finally {
      setSubmitting(false);
    }
  }

  async function remove(roleId: string) {
    setError(null);
    try {
      await deleteRole(roleId);
      load();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Unable to delete the role.');
    }
  }

  return (
    <Page>
      <PageHeader
        title="Roles"
        description="Create roles, assign permissions, and grant them to teams."
      />
      <Card>
        <CardHeader>
          <CardTitle>New role</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={(event) => void create(event)}>
            <Block direction="row" gap="md">
              <input
                type="text"
                placeholder="Role name"
                value={name()}
                onInput={(event: Event) => setName((event.target as HTMLInputElement).value)}
                required
              />
              <Button variant="primary" type="submit" disabled={submitting()}>
                {submitting() ? 'Creating…' : 'Create role'}
              </Button>
            </Block>
          </form>
        </CardContent>
      </Card>
      {error() ? <p role="alert">{error()}</p> : null}
      {roles() === null && !error() ? <p>Loading…</p> : null}
      <Block direction="column" gap="md">
        {(roles() ?? []).map((role) => (
          <Card key={role.roleId}>
            <CardContent>
              <Block direction="row" gap="md" align="center" justify="between">
                <Button asChild variant="ghost">
                  <a href={`/roles/${role.roleId}`}>{role.name}</a>
                </Button>
                <Button variant="destructive" onPress={() => void remove(role.roleId)}>
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
