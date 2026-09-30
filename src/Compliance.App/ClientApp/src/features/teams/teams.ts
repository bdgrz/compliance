import { createApiClient } from '../../api-client/index.js';
import { requireActiveTenantId } from '../tenants/tenants.js';

const client = createApiClient();

export interface TeamSummary {
  teamId: string;
  name: string;
}

export interface TeamMemberSummary {
  memberId: string;
}

export async function listTeams(): Promise<TeamSummary[]> {
  const tenantId = requireActiveTenantId();
  const teams: TeamSummary[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listTeams({ params: { tenant_id: tenantId }, query: { cursor } });
    if (!result.ok) {
      throw new Error(describeFailure(result));
    }

    for (const item of result.data?.items ?? []) {
      if (item) {
        teams.push({ teamId: item.team_id, name: item.name });
      }
    }

    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);

  return teams;
}

export async function getTeam(teamId: string): Promise<TeamSummary | null> {
  const tenantId = requireActiveTenantId();
  const result = await client.getTeam({ params: { tenant_id: tenantId, team_id: teamId } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }

  return result.data ? { teamId: result.data.team_id, name: result.data.name } : null;
}

export async function defineTeam(name: string): Promise<string> {
  const tenantId = requireActiveTenantId();
  const teamId = crypto.randomUUID();
  const result = await client.defineTeam({ params: { tenant_id: tenantId, team_id: teamId }, body: { name } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }

  return teamId;
}

export async function deleteTeam(teamId: string): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.deleteTeam({ params: { tenant_id: tenantId, team_id: teamId } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }
}

export async function listTeamMembers(teamId: string): Promise<TeamMemberSummary[]> {
  const tenantId = requireActiveTenantId();
  const members: TeamMemberSummary[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listTeamMembers({ params: { tenant_id: tenantId, team_id: teamId }, query: { cursor } });
    if (!result.ok) {
      throw new Error(describeFailure(result));
    }

    for (const item of result.data?.items ?? []) {
      if (item) {
        members.push({ memberId: item.member_id });
      }
    }

    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);

  return members;
}

export async function assignTeamMember(teamId: string, memberId: string): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.assignTeamMember({ params: { tenant_id: tenantId, team_id: teamId, member_id: memberId } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }
}

export async function removeTeamMember(teamId: string, memberId: string): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.removeTeamMember({ params: { tenant_id: tenantId, team_id: teamId, member_id: memberId } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }
}

function describeFailure(result: { ok: false; kind: string; status?: number; error?: unknown }): string {
  if (result.status === 403) {
    return 'You do not have permission to change this.';
  }
  const detail =
    result.kind === 'http' && typeof result.error === 'object' && result.error !== null
      ? (result.error as { detail?: unknown }).detail
      : undefined;
  if (typeof detail === 'string' && detail.length > 0) {
    return detail;
  }
  return result.kind === 'http'
    ? `The request failed (${result.status ?? 'unknown status'}).`
    : 'The request could not be completed.';
}
