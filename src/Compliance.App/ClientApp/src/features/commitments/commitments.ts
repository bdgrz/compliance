import { createApiClient } from '../../api-client/index.js';
import { programFailure, ProgramRequestError } from '../programs/programs.js';
import { requireActiveTenantId } from '../tenants/tenants.js';

const client = createApiClient();

// CUECs and CSOCs are performed outside the service organization and never count as internal controls.
export const commitmentKinds = [
  { value: 'service_commitment', title: 'Service commitments', short: 'Service commitment', internal: true },
  { value: 'system_requirement', title: 'System requirements', short: 'System requirement', internal: true },
  {
    value: 'user_entity_responsibility',
    title: 'Complementary user entity controls (CUECs)',
    short: 'CUEC',
    internal: false,
  },
  {
    value: 'subservice_responsibility',
    title: 'Complementary subservice organization controls (CSOCs)',
    short: 'CSOC',
    internal: false,
  },
] as const;

export function kindTitle(kind: string): string {
  return commitmentKinds.find((item) => item.value === kind)?.short ?? label(kind);
}

export function isInternal(kind: string): boolean {
  return commitmentKinds.find((item) => item.value === kind)?.internal ?? true;
}

export function label(value: string | null | undefined): string {
  if (!value) return 'none';
  return value.replaceAll('_', ' ');
}

export interface CommitmentDraft {
  draftId: string;
  serviceId: string;
  kind: string;
  identifier: string;
  revision: number;
  status: string;
  sourceResolution: string;
  ownerResolution: string;
  applicabilityResolution: string;
  statement: string;
  context: string;
  sourceReference: string;
  lastChangedBy: string;
  lastChangedAt: string;
}

export interface CommitmentRevision {
  revision: number;
  statement: string;
  context: string;
  sourceReference: string;
  changedBy: string;
  changedByMemberId: string;
  changedAt: string;
}

export interface CommitmentDecision {
  decisionId: string;
  stage: string;
  revision: number;
  outcome: string;
  actor: string;
  actorMemberId: string;
  rationale: string;
  decidedAt: string;
  ownerReference: string | null;
  applicability: string | null;
  interpretation: string | null;
  interpretationNote: string | null;
  sourceVerification: string | null;
  sourceEvidence: string | null;
  effectiveFrom: string | null;
  version: number | null;
  waiverId: string | null;
}

export interface CommitmentVersion {
  version: number;
  revision: number;
  statement: string;
  context: string;
  sourceReference: string;
  ownerReference: string;
  applicability: string;
  interpretation: string;
  interpretationNote: string | null;
  performedBy: string;
  internallyPerformed: boolean;
  effectiveFrom: string;
  sourceResolution: string;
  sourceEvidence: string | null;
  reviewedBy: string;
  approvedBy: string | null;
}

export interface CommitmentImpact {
  revision: number;
  effectiveVersion: number | null;
  changes: { field: string; before: string | null; after: string | null }[];
  dependents: { context: string; recordType: string; recordId: string; relationship: string }[];
  unlinked: { context: string; reason: string }[];
  complete: boolean;
  digest: string;
}

export interface ServiceOption {
  serviceId: string;
  name: string;
}

export interface DraftInput {
  statement: string;
  context: string;
  sourceReference: string;
}

type Actor = { display: string } | null | undefined;
type DraftData = {
  draft_id: string;
  service_id: string;
  kind: string;
  identifier: string;
  revision: number | string;
  status: string;
  source_resolution: string;
  owner_resolution: string;
  applicability_resolution: string;
  statement: string;
  context: string;
  source_reference: string;
  last_changed_by_display: string;
  last_changed_at: string;
  last_changed_by?: Actor;
};
type DecisionData = {
  decision_id: string;
  revision: number | string;
  outcome: string;
  owner_reference: string | null;
  applicability: string | null;
  interpretation: string | null;
  interpretation_note: string | null;
  rationale: string;
  version: number | string | null;
  effective_from: unknown;
  actor_member_id: string;
  actor_display: string;
  decided_at: string;
  separation_of_duties_waiver_id: string | null;
  stage?: string;
  source_verification?: string | null;
  source_evidence?: string | null;
  actor?: Actor;
};
type VersionData = {
  version: number | string;
  revision: number | string;
  statement: string;
  context: string;
  source_reference: string;
  owner_reference: string;
  applicability: string;
  interpretation: string;
  interpretation_note: string | null;
  performed_by: string;
  internally_performed: boolean;
  effective_from: unknown;
  decision: DecisionData;
  source_resolution?: string;
  source_evidence?: string | null;
  approval?: DecisionData | null;
};

function toDraft(data: DraftData): CommitmentDraft {
  return {
    draftId: data.draft_id,
    serviceId: data.service_id,
    kind: data.kind,
    identifier: data.identifier,
    revision: Number(data.revision),
    status: data.status,
    sourceResolution: data.source_resolution,
    ownerResolution: data.owner_resolution,
    applicabilityResolution: data.applicability_resolution,
    statement: data.statement,
    context: data.context,
    sourceReference: data.source_reference,
    lastChangedBy: data.last_changed_by?.display ?? data.last_changed_by_display,
    lastChangedAt: data.last_changed_at,
  };
}

function toDecision(data: DecisionData): CommitmentDecision {
  return {
    decisionId: data.decision_id,
    stage: data.stage ?? 'review',
    revision: Number(data.revision),
    outcome: data.outcome,
    actor: data.actor?.display ?? data.actor_display,
    actorMemberId: data.actor_member_id,
    rationale: data.rationale,
    decidedAt: data.decided_at,
    ownerReference: data.owner_reference,
    applicability: data.applicability,
    interpretation: data.interpretation,
    interpretationNote: data.interpretation_note,
    sourceVerification: data.source_verification ?? null,
    sourceEvidence: data.source_evidence ?? null,
    effectiveFrom: typeof data.effective_from === 'string' ? data.effective_from : null,
    version: data.version === null ? null : Number(data.version),
    waiverId: data.separation_of_duties_waiver_id,
  };
}

function toVersion(data: VersionData): CommitmentVersion {
  return {
    version: Number(data.version),
    revision: Number(data.revision),
    statement: data.statement,
    context: data.context,
    sourceReference: data.source_reference,
    ownerReference: data.owner_reference,
    applicability: data.applicability,
    interpretation: data.interpretation,
    interpretationNote: data.interpretation_note,
    performedBy: data.performed_by,
    internallyPerformed: data.internally_performed,
    effectiveFrom: typeof data.effective_from === 'string' ? data.effective_from : '',
    sourceResolution: data.source_resolution ?? 'unverified',
    sourceEvidence: data.source_evidence ?? null,
    reviewedBy: data.decision.actor?.display ?? data.decision.actor_display,
    approvedBy: data.approval ? (data.approval.actor?.display ?? data.approval.actor_display) : null,
  };
}

function scope(programId: string, draftId: string) {
  return { tenant_id: requireActiveTenantId(), program_id: programId, draft_id: draftId };
}

export async function listCommitmentDrafts(programId: string): Promise<CommitmentDraft[]> {
  const tenantId = requireActiveTenantId();
  const drafts: CommitmentDraft[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listCommitmentDrafts({
      params: { tenant_id: tenantId, program_id: programId },
      query: { cursor, limit: 200 },
    });
    if (!result.ok) throw programFailure(result, 'load the commitments');
    for (const item of result.data?.items ?? []) {
      if (item) drafts.push(toDraft(item));
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return drafts;
}

export async function listServiceOptions(programId: string): Promise<ServiceOption[]> {
  const tenantId = requireActiveTenantId();
  const services: ServiceOption[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listProgramClientServices({
      params: { tenant_id: tenantId, program_id: programId },
      query: { cursor, limit: 200 },
    });
    if (!result.ok) throw programFailure(result, 'load the services');
    for (const item of result.data?.items ?? []) {
      if (item && item.status === 'active') services.push({ serviceId: item.service_id, name: item.name });
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return services;
}

// `minimumRevision` asks the server to wait for (or report lag behind) a revision this client just wrote.
export async function getCommitmentDraft(
  programId: string,
  draftId: string,
  minimumRevision?: number
): Promise<CommitmentDraft> {
  const result = await client.getCommitmentDraft({
    params: scope(programId, draftId),
    query: { minimum_revision: minimumRevision },
  });
  if (!result.ok) throw programFailure(result, 'load this commitment');
  if (!result.data) throw new ProgramRequestError('This commitment was not found.', 404, false);
  return toDraft(result.data);
}

export async function createCommitmentDraft(
  programId: string,
  input: DraftInput & { serviceId: string; kind: string; identifier: string }
): Promise<string> {
  const tenantId = requireActiveTenantId();
  const result = await client.createCommitmentDraft({
    params: { tenant_id: tenantId, program_id: programId },
    body: {
      service_id: input.serviceId,
      kind: input.kind,
      identifier: input.identifier.trim(),
      statement: input.statement.trim(),
      context: input.context.trim(),
      source_reference: input.sourceReference.trim(),
    },
  });
  if (!result.ok) throw programFailure(result, 'create the commitment');
  if (!result.data) throw new ProgramRequestError('The commitment was not created.', null, false);
  return result.data.draft_id;
}

export async function reviseCommitmentDraft(
  programId: string,
  draftId: string,
  expectedRevision: number,
  input: DraftInput
): Promise<void> {
  const result = await client.reviseCommitmentDraft({
    params: scope(programId, draftId),
    body: {
      expected_revision: expectedRevision,
      statement: input.statement.trim(),
      context: input.context.trim(),
      source_reference: input.sourceReference.trim(),
    },
  });
  if (!result.ok) throw programFailure(result, 'save the commitment draft');
}

export async function listCommitmentRevisions(programId: string, draftId: string): Promise<CommitmentRevision[]> {
  const revisions: CommitmentRevision[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listCommitmentDraftRevisions({
      params: scope(programId, draftId),
      query: { cursor, limit: 200 },
    });
    if (!result.ok) throw programFailure(result, 'load the commitment history');
    for (const item of result.data?.items ?? []) {
      if (item)
        revisions.push({
          revision: Number(item.revision),
          statement: item.statement,
          context: item.context,
          sourceReference: item.source_reference,
          changedBy: item.actor?.display ?? item.changed_by_display,
          changedByMemberId: item.changed_by_member_id,
          changedAt: item.changed_at,
        });
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return revisions.sort((a, b) => b.revision - a.revision);
}

export async function listCommitmentDecisions(programId: string, draftId: string): Promise<CommitmentDecision[]> {
  const decisions: CommitmentDecision[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listCommitmentDecisions({
      params: scope(programId, draftId),
      query: { cursor, limit: 200 },
    });
    if (!result.ok) throw programFailure(result, 'load the commitment decisions');
    for (const item of result.data?.items ?? []) {
      if (item) decisions.push(toDecision(item));
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return decisions.sort((a, b) => b.decidedAt.localeCompare(a.decidedAt));
}

export async function listCommitmentVersions(programId: string, draftId: string): Promise<CommitmentVersion[]> {
  const versions: CommitmentVersion[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listCommitmentVersions({
      params: scope(programId, draftId),
      query: { cursor, limit: 200 },
    });
    if (!result.ok) throw programFailure(result, 'load the commitment versions');
    for (const item of result.data?.items ?? []) {
      if (item) versions.push(toVersion(item));
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return versions.sort((a, b) => b.version - a.version);
}

// Returns null when no version is effective on the given date.
export async function getEffectiveCommitmentVersion(
  programId: string,
  draftId: string,
  effectiveOn: string
): Promise<CommitmentVersion | null> {
  const result = await client.getEffectiveCommitmentVersion({
    params: scope(programId, draftId),
    query: { effective_on: effectiveOn },
  });
  if (!result.ok) {
    if (result.status === 404) return null;
    throw programFailure(result, 'load the effective commitment version');
  }
  return result.data ? toVersion(result.data) : null;
}

export async function previewCommitmentImpact(
  programId: string,
  draftId: string,
  expectedRevision: number
): Promise<CommitmentImpact> {
  const result = await client.previewCommitmentImpact({
    params: scope(programId, draftId),
    query: { expected_revision: expectedRevision },
  });
  if (!result.ok) throw programFailure(result, 'preview the commitment impact');
  const data = result.data;
  if (!data) throw new ProgramRequestError('The impact preview is not available.', 404, false);
  return {
    revision: Number(data.revision),
    effectiveVersion: data.effective_version === null ? null : Number(data.effective_version),
    changes: (data.changes ?? []).filter((item) => item !== null),
    dependents: (data.dependents ?? [])
      .filter((item) => item !== null)
      .map((item) => ({
        context: item.context,
        recordType: item.record_type,
        recordId: item.record_id,
        relationship: item.relationship,
      })),
    unlinked: (data.unlinked_contexts ?? []).filter((item) => item !== null),
    complete: data.complete,
    digest: data.digest,
  };
}

export interface ReviewInput {
  outcome: 'accept' | 'request_changes';
  rationale: string;
  ownerReference: string;
  applicability: string;
  interpretation: string;
  interpretationNote: string;
  sourceVerifiedReference: string;
  sourceEvidence: string;
  waiverId?: string;
}

export async function reviewCommitmentDraft(
  programId: string,
  draftId: string,
  expectedRevision: number,
  input: ReviewInput
): Promise<void> {
  const accept = input.outcome === 'accept';
  const result = await client.reviewCommitmentDraft({
    params: scope(programId, draftId),
    body: {
      expected_revision: expectedRevision,
      outcome: input.outcome,
      rationale: input.rationale.trim(),
      ...(accept
        ? {
            owner_reference: input.ownerReference.trim(),
            applicability: input.applicability,
            interpretation: input.interpretation,
            interpretation_note: input.interpretationNote.trim() || null,
            source_verified_reference: input.sourceVerifiedReference.trim(),
            source_evidence: input.sourceEvidence.trim(),
          }
        : {}),
      ...(input.waiverId ? { separation_of_duties_waiver_id: input.waiverId } : {}),
    },
  });
  if (!result.ok) throw programFailure(result, 'review this commitment');
}

export async function approveCommitmentDraft(
  programId: string,
  draftId: string,
  approval: {
    expectedRevision: number;
    acceptedReviewDecisionId: string;
    effectiveFrom: string;
    rationale: string;
    impactDigest: string;
    waiverId?: string;
  }
): Promise<void> {
  const result = await client.approveCommitmentDraft({
    params: scope(programId, draftId),
    body: {
      expected_revision: approval.expectedRevision,
      accepted_review_decision_id: approval.acceptedReviewDecisionId,
      effective_from: approval.effectiveFrom,
      rationale: approval.rationale.trim(),
      impact_digest: approval.impactDigest,
      ...(approval.waiverId ? { separation_of_duties_waiver_id: approval.waiverId } : {}),
    },
  });
  if (!result.ok) throw programFailure(result, 'approve this commitment');
}

// The latest review of this exact revision, when it accepted the revision and nothing approved it yet.
export function acceptedReviewFor(decisions: CommitmentDecision[], revision: number): CommitmentDecision | null {
  const latest = decisions.find((decision) => decision.revision === revision);
  return latest?.stage === 'review' && latest.outcome === 'accept' ? latest : null;
}

// Members who authored any revision since the last effective version; they cannot review or approve.
export function pendingAuthors(revisions: CommitmentRevision[], versions: CommitmentVersion[]): string[] {
  const effectiveRevision = versions.length === 0 ? 0 : Math.max(...versions.map((item) => item.revision));
  const names = revisions.filter((item) => item.revision > effectiveRevision).map((item) => item.changedBy);
  return [...new Set(names)];
}

export function describeCommitmentFailure(error: Error, subject = 'this commitment'): string {
  if (error instanceof ProgramRequestError && error.status === 403) {
    return `You do not have permission to change ${subject}.`;
  }
  if (error instanceof ProgramRequestError && error.status === 409 && !error.transient) {
    return `Someone else changed ${subject} since you opened it (${error.message}). Reload to see their changes, then try again.`;
  }
  return error.message;
}

export { ProgramRequestError };
