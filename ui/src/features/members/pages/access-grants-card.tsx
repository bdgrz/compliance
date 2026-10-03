import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  Stack,
} from '@askrjs/themes/components';

import { listRoles } from '../../roles/roles.js';
import {
  grantMemberAccess,
  listGrantScopes,
  revokeAccessGrant,
  type MemberAccess,
} from '../members.js';

type GrantPath = NonNullable<MemberAccess['grant_paths'][number]>;

export function scopeLabel(scope: { kind: string; id: string }, scopes: { id: string; label: string }[] = []) {
  return (
    scopes.find((option) => option.id === scope.id)?.label ??
    (scope.kind === 'organization' ? 'Whole organization' : `${scope.kind} ${scope.id.slice(0, 8)}`)
  );
}

export function grantStatus(path: GrantPath, now = Date.now()) {
  if (path.grant.revoked_at) return 'revoked';
  const until = path.grant.terms.effective_until;
  if (until && Date.parse(until) <= now) return 'expired';
  if (Date.parse(path.grant.terms.effective_from) > now) return 'scheduled';
  return path.is_effective ? 'active' : 'inactive';
}

export function AccessGrantsCard({ access, onChanged }: { access: MemberAccess; onChanged: () => void }) {
  const [roleId, setRoleId] = state('');
  const [scopeId, setScopeId] = state('');
  const [until, setUntil] = state('');
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<string | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  const roles = resource(() => listRoles(), []);
  const scopes = resource(() => listGrantScopes(), []);
  const grantPaths = access.grant_paths.filter((path): path is GrantPath => path !== null);

  async function change(action: () => Promise<void>, done: string) {
    setActionError(null);
    setNotice(null);
    setPending(true);
    try {
      await action();
      setNotice(done);
      onChanged();
    } catch (failure) {
      setActionError(failure instanceof Error ? failure.message : 'The change could not be saved.');
    } finally {
      setPending(false);
    }
  }

  function grant(event: Event) {
    event.preventDefault();
    const scope = (scopes.value ?? []).find((option) => option.id === scopeId());
    const role = (roles.value ?? []).find((option) => option.roleId === roleId());
    if (!scope || !role) return;
    const effectiveUntil = until() ? new Date(`${until()}T23:59:59Z`).toISOString() : null;
    void change(
      () => grantMemberAccess(access.member_id, role.roleId, scope, effectiveUntil),
      `Granted ${role.name} on ${scope.label}.`
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Through access grants</CardTitle>
        <CardDescription>
          A grant gives this member a role on the whole organization or on one program. A grant on
          the organization covers every program in it.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Stack gap="md">
          {actionError() ? <p role="alert">{actionError()}</p> : null}
          {notice() ? <p role="status">{notice()}</p> : null}
          {grantPaths.length === 0 ? (
            <p>No access grants apply to this member.</p>
          ) : (
            <ul className="plain-list">
              {grantPaths.map((path) => {
                const status = grantStatus(path);
                return (
                  <li className="grant-row">
                    <span>
                      <strong>{path.role_name}</strong> on{' '}
                      {scopeLabel(path.grant.terms.scope, scopes.value ?? [])} ·{' '}
                      {path.grant.terms.source.kind === 'manual'
                        ? 'granted directly'
                        : `from ${path.grant.terms.source.kind.replaceAll('_', ' ')}`}{' '}
                      · granted by{' '}
                      {path.grant.terms.granted_by.display} · from{' '}
                      {new Date(path.grant.terms.effective_from).toLocaleDateString()}
                      {path.grant.terms.effective_until
                        ? ` until ${new Date(path.grant.terms.effective_until).toLocaleDateString()}`
                        : ''}{' '}
                      · <span className="grant-status">{status}</span>
                    </span>
                    {status === 'active' || status === 'scheduled' ? (
                      <Button
                        variant="destructive"
                        size="sm"
                        disabled={pending()}
                        aria-label={`Revoke ${path.role_name} on ${scopeLabel(path.grant.terms.scope, scopes.value ?? [])}`}
                        onPress={() =>
                          void change(
                            () => revokeAccessGrant(path.grant.grant_id),
                            `Revoked ${path.role_name}. The member lost the access it provided.`
                          )
                        }
                      >
                        Revoke
                      </Button>
                    ) : null}
                  </li>
                );
              })}
            </ul>
          )}
          {roles.error || scopes.error ? (
            <Stack gap="sm">
              <p role="alert">{(roles.error ?? scopes.error)!.message}</p>
              <Button
                variant="secondary"
                onPress={() => {
                  roles.refresh();
                  scopes.refresh();
                }}
              >
                Try again
              </Button>
            </Stack>
          ) : (
            <form onSubmit={grant}>
              <Stack gap="sm">
                <label className="registration-field">
                  <span>Role</span>
                  <select
                    value={roleId()}
                    onChange={(event: Event) => setRoleId((event.target as HTMLSelectElement).value)}
                    required
                  >
                    <option value="">Choose a role</option>
                    {(roles.value ?? []).map((role) => (
                      <option value={role.roleId}>{role.name}</option>
                    ))}
                  </select>
                </label>
                <label className="registration-field">
                  <span>Scope</span>
                  <select
                    value={scopeId()}
                    onChange={(event: Event) => setScopeId((event.target as HTMLSelectElement).value)}
                    required
                  >
                    <option value="">Choose a scope</option>
                    {(scopes.value ?? []).map((scope) => (
                      <option value={scope.id}>{scope.label}</option>
                    ))}
                  </select>
                </label>
                <label className="registration-field">
                  <span>Ends on (optional)</span>
                  <input
                    type="date"
                    value={until()}
                    onInput={(event: Event) => setUntil((event.target as HTMLInputElement).value)}
                  />
                </label>
                <Button variant="primary" type="submit" disabled={pending()}>
                  {pending() ? 'Granting…' : 'Grant access'}
                </Button>
              </Stack>
            </form>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
}
