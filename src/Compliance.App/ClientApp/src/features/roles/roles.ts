import { createApiClient } from '../../api-client/index.js';
import { requireActiveTenantId } from '../tenants/tenants.js';

const client = createApiClient();

export interface RoleSummary {
  roleId: string;
  name: string;
}

export interface RolePermissionSummary {
  permission: string;
}

export interface RoleTeamSummary {
  teamId: string;
}

export async function listRoles(): Promise<RoleSummary[]> {
  const tenantId = requireActiveTenantId();
  const roles: RoleSummary[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listRoles({ params: { tenant_id: tenantId }, query: { cursor } });
    if (!result.ok) {
      throw new Error(describeFailure(result));
    }

    for (const item of result.data?.items ?? []) {
      if (item) {
        roles.push({ roleId: item.role_id, name: item.name });
      }
    }

    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);

  return roles;
}

export async function getRole(roleId: string): Promise<RoleSummary | null> {
  const tenantId = requireActiveTenantId();
  const result = await client.getRole({ params: { tenant_id: tenantId, role_id: roleId } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }

  return result.data ? { roleId: result.data.role_id, name: result.data.name } : null;
}

export async function defineRole(name: string): Promise<string> {
  const tenantId = requireActiveTenantId();
  const roleId = crypto.randomUUID();
  const result = await client.defineRole({ params: { tenant_id: tenantId, role_id: roleId }, body: { name } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }

  return roleId;
}

export async function deleteRole(roleId: string): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.deleteRole({ params: { tenant_id: tenantId, role_id: roleId } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }
}

export async function listRolePermissions(roleId: string): Promise<RolePermissionSummary[]> {
  const tenantId = requireActiveTenantId();
  const permissions: RolePermissionSummary[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listRolePermissions({ params: { tenant_id: tenantId, role_id: roleId }, query: { cursor } });
    if (!result.ok) {
      throw new Error(describeFailure(result));
    }

    for (const item of result.data?.items ?? []) {
      if (item) {
        permissions.push({ permission: item.permission });
      }
    }

    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);

  return permissions;
}

export async function assignRolePermission(roleId: string, permission: string): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.assignRolePermission({ params: { tenant_id: tenantId, role_id: roleId, permission } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }
}

export async function removeRolePermission(roleId: string, permission: string): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.removeRolePermission({ params: { tenant_id: tenantId, role_id: roleId, permission } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }
}

export async function listRoleTeams(roleId: string): Promise<RoleTeamSummary[]> {
  const tenantId = requireActiveTenantId();
  const teams: RoleTeamSummary[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listRoleTeams({ params: { tenant_id: tenantId, role_id: roleId }, query: { cursor } });
    if (!result.ok) {
      throw new Error(describeFailure(result));
    }

    for (const item of result.data?.items ?? []) {
      if (item) {
        teams.push({ teamId: item.team_id });
      }
    }

    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);

  return teams;
}

export async function assignTeamRole(roleId: string, teamId: string): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.assignTeamRole({ params: { tenant_id: tenantId, team_id: teamId, role_id: roleId } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }
}

export async function removeTeamRole(roleId: string, teamId: string): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.removeTeamRole({ params: { tenant_id: tenantId, team_id: teamId, role_id: roleId } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }
}

function describeFailure(result: { ok: false; kind: string; status?: number }): string {
  return result.kind === 'http'
    ? `The request failed (${result.status ?? 'unknown status'}).`
    : 'The request could not be completed.';
}
