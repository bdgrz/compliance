import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
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

import { listRoles, type RoleSummary } from '../../roles/roles.js';
import { listTeams, type TeamSummary } from '../../teams/teams.js';
import {
  listAccessGrants,
  listGrantScopes,
  MemberRequestError,
  revokeAccessGrant,
  type AccessGrantRecord,
  type ScopeOption,
} from '../members.js';
import { scopeLabel } from './access-grants-card.js';

// Status from the grant's own recorded interval and revocation; the server stays the authority on
// whether it currently authorizes anything.
export function recordStatus(grant: AccessGrantRecord, now = Date.now()): string {
  if (grant.revoked_at) return 'revoked';
  const until = grant.terms.effective_until;
  if (until && Date.parse(until) <= now) return 'expired';
  if (Date.parse(grant.terms.effective_from) > now) return 'scheduled';
  return 'active';
}

export function sourceLabel(source: { kind: string }): string {
  return source.kind === 'manual' ? 'Granted directly' : source.kind.replaceAll('_', ' ');
}

const statusFilters = [
  { value: 'current', label: 'Active and scheduled' },
  { value: 'all', label: 'All, including revoked and expired' },
];

export function AccessGrantsPage() {
  const [version, setVersion] = state(0);
  const [filter, setFilter] = state('current');
  const [pending, setPending] = state<string | null>(null);
  const [actionError, setActionError] = state<string | null>(null);
  const [notice, setNotice] = state<string | null>(null);
  const grants = resource(() => listAccessGrants(), [version()]);
  const roles = resource(() => listRoles().catch(() => [] as RoleSummary[]), []);
  const teams = resource(() => listTeams().catch(() => [] as TeamSummary[]), []);
  const scopes = resource(() => listGrantScopes().catch(() => [] as ScopeOption[]), []);

  if (grants.error instanceof MemberRequestError && grants.error.status === 403) {
    return (
      <Page>
        <EmptyState
          title="Access grants are not available to you"
          titleAs="h1"
          description="Only organization administrators can review access grants. Ask an Org Admin if you need access."
        />
      </Page>
    );
  }

  const roleNames = new Map((roles.value ?? []).map((role) => [role.roleId, role.name]));
  const teamNames = new Map((teams.value ?? []).map((team) => [team.teamId, team.name]));
  const all = grants.value ?? [];
  const items = filter() === 'all' ? all : all.filter((grant) => ['active', 'scheduled'].includes(recordStatus(grant)));

  function principal(grant: AccessGrantRecord): string {
    const { kind, id } = grant.terms.principal;
    return kind === 'team' ? `Team ${teamNames.get(id) ?? id.slice(0, 8)}` : `Member ${id.slice(0, 8)}`;
  }

  async function revoke(grant: AccessGrantRecord) {
    setActionError(null);
    setNotice(null);
    setPending(grant.grant_id);
    try {
      await revokeAccessGrant(grant.grant_id);
      setNotice(`Revoked ${roleNames.get(grant.terms.role_id) ?? 'the role'} from ${principal(grant)}.`);
      setVersion(version() + 1);
    } catch (failure) {
      setActionError(failure instanceof Error ? failure.message : 'The grant could not be revoked.');
    } finally {
      setPending(null);
    }
  }

  return (
    <Page>
      <PageHeader
        title="Access grants"
        description="Every role granted on the organization or one of its programs, with who granted it and when it applies."
      />
      <Card>
        <CardHeader>
          <CardTitle>Grants</CardTitle>
          <CardDescription>
            Grant access from a member's page. Engagement access for firm staff comes only from an accepted
            engagement assignment, which this list does not show yet.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Stack gap="sm">
            {actionError() ? <p role="alert">{actionError()}</p> : null}
            {notice() ? <p role="status">{notice()}</p> : null}
            <label className="registration-field">
              <span>Show</span>
              <select value={filter()} onChange={(event: Event) => setFilter((event.target as HTMLSelectElement).value)}>
                {statusFilters.map((option) => (
                  <option value={option.value}>{option.label}</option>
                ))}
              </select>
            </label>
            {grants.pending && !grants.value ? (
              <Spinner label="Loading access grants" />
            ) : grants.error ? (
              <Stack gap="sm">
                <p role="alert">{grants.error.message}</p>
                <Button variant="secondary" onPress={() => grants.refresh()}>
                  Try again
                </Button>
              </Stack>
            ) : items.length === 0 ? (
              <p>{filter() === 'all' ? 'No access grants have been made.' : 'No active or scheduled access grants.'}</p>
            ) : (
              <table className="inventory-table">
                <caption className="visually-hidden">Access grants</caption>
                <thead>
                  <tr>
                    <th scope="col">Principal</th>
                    <th scope="col">Role</th>
                    <th scope="col">Scope</th>
                    <th scope="col">Source</th>
                    <th scope="col">Granted by</th>
                    <th scope="col">Effective</th>
                    <th scope="col">Status</th>
                    <th scope="col">
                      <span className="visually-hidden">Actions</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((grant) => {
                    const status = recordStatus(grant);
                    const role = roleNames.get(grant.terms.role_id) ?? 'Role';
                    return (
                      <tr>
                        <td>{principal(grant)}</td>
                        <td>{role}</td>
                        <td>{scopeLabel(grant.terms.scope, scopes.value ?? [])}</td>
                        <td>{sourceLabel(grant.terms.source)}</td>
                        <td>{grant.terms.granted_by.display}</td>
                        <td>
                          {new Date(grant.terms.effective_from).toLocaleDateString()}
                          {grant.terms.effective_until
                            ? ` – ${new Date(grant.terms.effective_until).toLocaleDateString()}`
                            : ' onward'}
                        </td>
                        <td>
                          <span className="grant-status">{status}</span>
                          {grant.revoked_by ? ` by ${grant.revoked_by.display}` : ''}
                        </td>
                        <td>
                          {status === 'active' || status === 'scheduled' ? (
                            <Button
                              variant="destructive"
                              size="sm"
                              disabled={pending() !== null}
                              aria-label={`Revoke ${role} from ${principal(grant)}`}
                              onPress={() => void revoke(grant)}
                            >
                              {pending() === grant.grant_id ? 'Revoking…' : 'Revoke'}
                            </Button>
                          ) : null}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </Stack>
        </CardContent>
      </Card>
    </Page>
  );
}
