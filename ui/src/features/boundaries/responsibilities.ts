import { createApiClient } from '../../api-client/index.js';
import { programFailure, ProgramRequestError } from '../programs/programs.js';
import { requireActiveTenantId } from '../tenants/tenants.js';

const client = createApiClient();

export const responsibilityTypes = [
  'control_owner',
  'evidence_contributor',
  'assigned_reviewer',
  'access_reviewer',
  'corrective_action_owner',
  'policy_approver',
] as const;
export type ResponsibilityType = (typeof responsibilityTypes)[number];

// The exact record revision a responsibility, conflict, or waiver applies to.
export interface ResponsibilityScope {
  recordType: string;
  recordId: string;
  versionId: string;
  revision: number;
}

export interface Assignment {
  assignmentId: string;
  memberId: string;
  type: string;
  assignedBy: string;
  assignedAt: string;
  effectiveFrom: string;
  effectiveUntil: string | null;
  revokedAt: string | null;
  revokedBy: string | null;
  revocationReason: string | null;
  waiverIds: string[];
}

export interface Conflict {
  kind: string;
  memberId: string;
  existingType: string;
  proposedType: string;
  waiverAction: string;
}

export interface MemberOption {
  userId: string;
  label: string;
}

export interface Waiver {
  waiverId: string;
  scope: ResponsibilityScope & { action: string };
  beneficiaryMemberId: string;
  requester: string;
  requesterMemberId: string;
  rationale: string;
  requestedAt: string;
  expiresAt: string;
  approver: string | null;
  approverMemberId: string | null;
  approvedAt: string | null;
  status: string;
  active: boolean;
}

function optionalDate(value: unknown): string | null {
  return typeof value === 'string' ? value : null;
}

export async function listAssignments(
  scope: ResponsibilityScope,
  minimumRevision?: number
): Promise<{ revision: number; assignments: Assignment[] }> {
  const tenantId = requireActiveTenantId();
  const result = await client.listResponsibilities({
    params: { tenant_id: tenantId },
    query: {
      record_type: scope.recordType,
      record_id: scope.recordId,
      version_id: scope.versionId,
      scope_revision: scope.revision,
      minimum_revision: minimumRevision,
    },
  });
  if (!result.ok) throw programFailure(result, 'load the responsibilities');
  return {
    revision: Number(result.data?.revision ?? 0),
    assignments: (result.data?.assignments ?? [])
      .filter((item) => item !== null)
      .map((item) => ({
        assignmentId: item.assignment_id,
        memberId: item.member_id,
        type: item.type,
        assignedBy: item.assigned_by_display ?? 'a member',
        assignedAt: item.assigned_at,
        effectiveFrom: item.effective_from,
        effectiveUntil: optionalDate(item.effective_until),
        revokedAt: optionalDate(item.revoked_at),
        revokedBy: item.revoked_by_display ?? null,
        revocationReason: item.revocation_reason ?? null,
        waiverIds: (item.separation_of_duties_waiver_ids ?? []).filter((id) => id !== null),
      })),
  };
}

export async function previewConflicts(
  scope: ResponsibilityScope,
  memberUserId: string,
  type: string,
  effectiveFrom: string
): Promise<{ memberId: string | null; conflicts: Conflict[] }> {
  const tenantId = requireActiveTenantId();
  const result = await client.previewResponsibilityConflicts({
    params: { tenant_id: tenantId },
    query: {
      member_user_id: memberUserId,
      type,
      record_type: scope.recordType,
      record_id: scope.recordId,
      version_id: scope.versionId,
      scope_revision: scope.revision,
      effective_from: effectiveFrom,
    },
  });
  if (!result.ok) throw programFailure(result, 'preview responsibility conflicts');
  const conflicts = (result.data?.conflicts ?? [])
    .filter((item) => item !== null)
    .map((item) => ({
      kind: item.kind,
      memberId: item.member_id,
      existingType: item.existing_type,
      proposedType: item.proposed_type,
      waiverAction: item.waiver_action,
    }));
  return { memberId: conflicts[0]?.memberId ?? null, conflicts };
}

export async function assignResponsibility(
  scope: ResponsibilityScope,
  memberUserId: string,
  type: ResponsibilityType,
  effectiveFrom: string,
  waiverIds: string[]
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.assignResponsibility({
    params: { tenant_id: tenantId },
    body: {
      member_user_id: memberUserId,
      type,
      record_type: scope.recordType,
      record_id: scope.recordId,
      version_id: scope.versionId,
      scope_revision: scope.revision,
      effective_from: effectiveFrom,
      separation_of_duties_waiver_ids: waiverIds,
    },
  });
  if (!result.ok) throw programFailure(result, 'assign the responsibility');
}

export async function revokeResponsibility(scope: ResponsibilityScope, assignmentId: string, reason: string): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.revokeResponsibility({
    params: { tenant_id: tenantId, assignment_id: assignmentId },
    body: {
      record_type: scope.recordType,
      record_id: scope.recordId,
      version_id: scope.versionId,
      scope_revision: scope.revision,
      reason: reason.trim(),
    },
  });
  if (!result.ok) throw programFailure(result, 'revoke the responsibility');
}

export async function listMemberOptions(): Promise<MemberOption[]> {
  const tenantId = requireActiveTenantId();
  const options: MemberOption[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listTenantMembers({ params: { tenant_id: tenantId }, query: { cursor } });
    if (!result.ok) throw programFailure(result, 'load the members');
    for (const item of result.data?.items ?? []) {
      if (item && !item.is_suspended && item.affiliation !== 'firm_staff') {
        options.push({ userId: item.user_id, label: item.verified_email_address ?? item.user_id });
      }
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return options;
}

type WaiverData = {
  waiver_id: string;
  scope: { record_type: string; record_id: string; version_id: string; revision: number | string; action: string };
  beneficiary_member_id: string;
  requester_member_id: string;
  requester_display: string;
  requester?: { display: string } | null;
  rationale: string;
  requested_at: string;
  expires_at: string;
  approver_member_id: string | null;
  approver_display: string | null;
  approver?: { display: string } | null;
  approved_at?: unknown;
  status: string;
  active: boolean;
};

function toWaiver(data: WaiverData): Waiver {
  return {
    waiverId: data.waiver_id,
    scope: {
      recordType: data.scope.record_type,
      recordId: data.scope.record_id,
      versionId: data.scope.version_id,
      revision: Number(data.scope.revision),
      action: data.scope.action,
    },
    beneficiaryMemberId: data.beneficiary_member_id,
    requester: data.requester?.display ?? data.requester_display,
    requesterMemberId: data.requester_member_id,
    rationale: data.rationale,
    requestedAt: data.requested_at,
    expiresAt: data.expires_at,
    approver: data.approver?.display ?? data.approver_display,
    approverMemberId: data.approver_member_id,
    approvedAt: optionalDate(data.approved_at),
    status: data.status,
    active: data.active,
  };
}

export async function recordWaiver(
  scope: ResponsibilityScope,
  action: string,
  beneficiaryUserId: string,
  rationale: string,
  expiresAt: string
): Promise<Waiver> {
  const tenantId = requireActiveTenantId();
  const result = await client.recordSeparationOfDutiesWaiver({
    params: { tenant_id: tenantId },
    body: {
      scope: {
        record_type: scope.recordType,
        record_id: scope.recordId,
        version_id: scope.versionId,
        revision: scope.revision,
        action,
      },
      beneficiary_user_id: beneficiaryUserId,
      rationale: rationale.trim(),
      expires_at: expiresAt,
    },
  });
  if (!result.ok) throw programFailure(result, 'record the exception');
  if (!result.data) throw new ProgramRequestError('The exception was not recorded.', null, false);
  return toWaiver(result.data);
}

export async function getWaiver(waiverId: string): Promise<Waiver> {
  const tenantId = requireActiveTenantId();
  const result = await client.getSeparationOfDutiesWaiver({ params: { tenant_id: tenantId, waiver_id: waiverId } });
  if (!result.ok) throw programFailure(result, 'load this exception');
  if (!result.data) throw new ProgramRequestError('This exception was not found.', 404, false);
  return toWaiver(result.data);
}

export async function approveWaiver(waiverId: string): Promise<Waiver> {
  const tenantId = requireActiveTenantId();
  const result = await client.approveSeparationOfDutiesWaiver({ params: { tenant_id: tenantId, waiver_id: waiverId } });
  if (!result.ok) throw programFailure(result, 'approve this exception');
  if (!result.data) throw new ProgramRequestError('The exception was not approved.', null, false);
  return toWaiver(result.data);
}

const workTypes = new Set(['control_owner', 'evidence_contributor', 'corrective_action_owner']);
const reviewTypes = new Set(['assigned_reviewer', 'access_reviewer']);

// Conflicts among the current (unrevoked) assignments, so review and approval actions can show
// them up front. The server remains the authority and re-checks every decision.
export function standingConflicts(assignments: Assignment[]): Conflict[] {
  const active = assignments.filter((assignment) => assignment.revokedAt === null);
  const conflicts: Conflict[] = [];
  for (const work of active.filter((assignment) => workTypes.has(assignment.type))) {
    for (const decision of active.filter((assignment) => assignment.memberId === work.memberId)) {
      if (reviewTypes.has(decision.type) || decision.type === 'policy_approver') {
        const review = reviewTypes.has(decision.type);
        conflicts.push({
          kind: review ? 'self_review' : 'self_approval',
          memberId: work.memberId,
          existingType: work.type,
          proposedType: decision.type,
          waiverAction: review ? 'review' : 'approve',
        });
      }
    }
  }
  return conflicts;
}

export function shortId(id: string): string {
  return id.slice(0, 8);
}
