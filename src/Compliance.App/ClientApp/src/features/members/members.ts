import { createApiClient } from '../../api-client/index.js';
import type { GetMemberAccessResponse200 } from '../../api-client/operations.js';
import { requireActiveTenantId } from '../tenants/tenants.js';

const client = createApiClient();

export interface BuiltInRole {
  value: string;
  label: string;
  explanation: string;
}

// The built-in client roles an administrator can invite into (M0-D03). Labels follow the accepted
// role names; the server remains the authority on what each role may do.
export const builtInRoles: readonly BuiltInRole[] = [
  {
    value: 'compliance_participation',
    label: 'Contributor',
    explanation: 'Owns and performs assigned controls, evidence, and tasks.',
  },
  {
    value: 'compliance_management',
    label: 'Compliance Lead',
    explanation: 'Runs the program: scope, controls, and approvals.',
  },
  {
    value: 'tenant_administration',
    label: 'Org Admin',
    explanation: 'Manages members, teams, access grants, and organization settings.',
  },
];

export function roleLabel(value: string | null | undefined): string {
  return builtInRoles.find((role) => role.value === value)?.label ?? value ?? 'No role';
}

export interface MemberSummary {
  userId: string;
  affiliation: string;
  suspended: boolean;
}

export interface InvitationSummary {
  emailAddress: string;
  builtInRole: string | null;
  administrator: boolean;
  status: string;
  expiresAt: string;
  deliveryStatus: string | null;
}

export type MemberAccess = NonNullable<GetMemberAccessResponse200>;

// Failures carry the HTTP status so pages can tell forbidden and conflict apart from other errors.
export class MemberRequestError extends Error {
  constructor(
    message: string,
    readonly status: number | null
  ) {
    super(message);
  }
}

export async function listMembers(): Promise<MemberSummary[]> {
  const tenantId = requireActiveTenantId();
  const members: MemberSummary[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listTenantMembers({ params: { tenant_id: tenantId }, query: { cursor } });
    if (!result.ok) throw failure(result, 'load the members');
    for (const item of result.data?.items ?? []) {
      if (item) {
        members.push({
          userId: item.user_id,
          affiliation: item.affiliation ?? 'client_personnel',
          suspended: item.is_suspended ?? false,
        });
      }
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return members;
}

export async function listInvitations(): Promise<InvitationSummary[]> {
  const tenantId = requireActiveTenantId();
  const invitations: InvitationSummary[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listTenantInvitations({ params: { tenant_id: tenantId }, query: { cursor } });
    if (!result.ok) throw failure(result, 'load the invitations');
    for (const item of result.data?.items ?? []) {
      if (item) {
        invitations.push({
          emailAddress: item.email_address,
          builtInRole: item.built_in_role,
          administrator: item.administrator,
          status: item.status,
          expiresAt: item.expires_at,
          deliveryStatus: item.delivery_status ?? null,
        });
      }
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return invitations;
}

export async function inviteMember(emailAddress: string, builtInRole: string): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.inviteOrganizationMember({
    params: { tenant_id: tenantId },
    body: { email_address: emailAddress, built_in_role: builtInRole },
  });
  if (!result.ok) throw failure(result, 'send the invitation');
}

export async function getMemberAccess(userId: string): Promise<MemberAccess> {
  const tenantId = requireActiveTenantId();
  const result = await client.getMemberAccess({ params: { tenant_id: tenantId, user_id: userId } });
  if (!result.ok) throw failure(result, "load this member's access");
  if (!result.data) throw new MemberRequestError('This member was not found.', 404);
  return result.data;
}

function failure(result: { ok: false; kind: string; status: number; error?: unknown }, action: string) {
  const detail =
    result.kind === 'http' && typeof result.error === 'object' && result.error !== null
      ? (result.error as { detail?: unknown }).detail
      : undefined;
  if (result.status === 403) {
    return new MemberRequestError(`You do not have permission to ${action}.`, 403);
  }
  return new MemberRequestError(
    typeof detail === 'string' && detail.length > 0 ? detail : `Unable to ${action}.`,
    result.kind === 'http' ? result.status : null
  );
}

export interface MembershipState {
  userId: string;
  affiliation: string;
  suspended: boolean;
  suspendedAt: string | null;
  suspendedBy: string | null;
  suspensionReason: string | null;
}

export interface OpenResponsibility {
  assignmentId: string;
  type: string;
  recordType: string;
  recordId: string;
  revision: string;
  assignedAt: string;
}

export async function getMembership(userId: string): Promise<MembershipState> {
  const tenantId = requireActiveTenantId();
  const result = await client.getTenantMember({ params: { tenant_id: tenantId, user_id: userId } });
  if (!result.ok) throw failure(result, 'load this member');
  if (!result.data) throw new MemberRequestError('This member was not found.', 404);
  return {
    userId: result.data.user_id,
    affiliation: result.data.affiliation ?? 'client_personnel',
    suspended: result.data.is_suspended ?? false,
    suspendedAt: result.data.suspended_at ?? null,
    suspendedBy: result.data.suspended_by_display ?? null,
    suspensionReason: result.data.suspension_reason ?? null,
  };
}

// Responsibilities still assigned to the member and not revoked or ended. When the member is
// suspended these are the orphaned work items that need a new owner.
export async function listOpenResponsibilities(
  userId: string,
  now = Date.now()
): Promise<OpenResponsibility[]> {
  const tenantId = requireActiveTenantId();
  const result = await client.listMemberResponsibilities({ params: { tenant_id: tenantId, user_id: userId } });
  if (!result.ok) throw failure(result, "load this member's responsibilities");
  return (result.data ?? [])
    .filter((item) => item !== null)
    .filter(
      (item) =>
        item.revoked_at === null &&
        (item.effective_until === null || Date.parse(item.effective_until) > now)
    )
    .map((item) => ({
      assignmentId: item.assignment_id,
      type: item.type,
      recordType: item.scope.record_type,
      recordId: item.scope.record_id,
      revision: String(item.scope.revision),
      assignedAt: item.assigned_at,
    }));
}

export async function suspendMember(userId: string, reason: string): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.suspendMember({ params: { tenant_id: tenantId, user_id: userId }, body: { reason } });
  if (!result.ok) throw failure(result, 'suspend this member');
}

export async function reinstateMember(userId: string): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.reinstateMember({ params: { tenant_id: tenantId, user_id: userId } });
  if (!result.ok) throw failure(result, 'reinstate this member');
}
