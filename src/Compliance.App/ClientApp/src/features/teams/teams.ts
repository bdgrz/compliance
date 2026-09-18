import { createApiClient } from '../../api-client/index.js';
import { readActiveTenant } from '../tenants/tenants.js';

const client = createApiClient();

export interface TeamSummary {
  teamId: string;
  name: string;
}

export interface TeamMemberSummary {
  memberId: string;
}

function requireTenantId(): string {
  const tenant = readActiveTenant();
  if (!tenant) {
    // Shouldn't happen in normal flow -- every authenticated page routes through
    // ensureActiveTenant() first. Send the user back to have that resolved again.
    window.location.assign('/');
    throw new Error('No active organization is selected.');
  }

  return tenant.tenantId;
}

export async function listTeams(): Promise<TeamSummary[]> {
  const tenantId = requireTenantId();
  const teams: TeamSummary[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listTeams({ params: { tenantId }, query: { cursor } });
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
  const tenantId = requireTenantId();
  const result = await client.getTeam({ params: { tenantId, teamId } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }

  return result.data ? { teamId: result.data.team_id, name: result.data.name } : null;
}

export async function defineTeam(name: string): Promise<string> {
  const tenantId = requireTenantId();
  const teamId = crypto.randomUUID();
  const result = await client.defineTeam({ params: { tenantId, teamId }, body: { name } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }

  return teamId;
}

export async function deleteTeam(teamId: string): Promise<void> {
  const tenantId = requireTenantId();
  const result = await client.deleteTeam({ params: { tenantId, teamId } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }
}

export async function listTeamMembers(teamId: string): Promise<TeamMemberSummary[]> {
  const tenantId = requireTenantId();
  const members: TeamMemberSummary[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listTeamMembers({ params: { tenantId, teamId }, query: { cursor } });
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
  const tenantId = requireTenantId();
  const result = await client.assignTeamMember({ params: { tenantId, teamId, memberId } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }
}

export async function removeTeamMember(teamId: string, memberId: string): Promise<void> {
  const tenantId = requireTenantId();
  const result = await client.removeTeamMember({ params: { tenantId, teamId, memberId } });
  if (!result.ok) {
    throw new Error(describeFailure(result));
  }
}

function describeFailure(result: { ok: false; kind: string; status?: number }): string {
  return result.kind === 'http'
    ? `The request failed (${result.status ?? 'unknown status'}).`
    : 'The request could not be completed.';
}
