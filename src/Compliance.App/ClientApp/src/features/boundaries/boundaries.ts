import { createApiClient } from '../../api-client/index.js';
import { programFailure, ProgramRequestError } from '../programs/programs.js';
import { requireActiveTenantId } from '../tenants/tenants.js';

const client = createApiClient();

export const entryKinds = ['inclusion', 'exclusion', 'assumption', 'question'] as const;
export const subjectTypes = [
  'service',
  'person',
  'application',
  'system_instance',
  'component',
  'information',
  'data_flow',
  'process',
  'location',
  'provider',
  'commitment',
] as const;
export const trustServicesCategories = [
  'security',
  'availability',
  'processing_integrity',
  'confidentiality',
  'privacy',
] as const;
export const engagementStages = ['readiness', 'type_i', 'type_ii'] as const;

export function label(value: string | null | undefined): string {
  if (!value) return 'none';
  if (value === 'type_i') return 'SOC 2 Type I';
  if (value === 'type_ii') return 'SOC 2 Type II';
  return value.replaceAll('_', ' ');
}

export interface ScopeEntry {
  entry_id: string;
  kind: string;
  subject_type: string;
  subject: string;
  governed_record_id: string | null;
  owner_reference: string;
  rationale: string;
  unresolved: boolean;
}

export interface BoundaryContent {
  statement: string;
  engagementStage: string;
  categories: string[];
  entries: ScopeEntry[];
}

export interface BoundaryVersion {
  versionId: string;
  revision: number;
  status: string;
  effectiveFrom: string | null;
  author: string;
  authorMemberId: string;
  changedAt: string;
  content: BoundaryContent;
}

export interface BoundaryDecision {
  decisionId: string;
  versionId: string;
  revision: number;
  outcome: string;
  actor: string;
  rationale: string;
  decidedAt: string;
  impactDigest: string | null;
  waiverId: string | null;
}

export interface Boundary {
  boundaryId: string;
  programId: string;
  draft: BoundaryVersion | null;
  approved: BoundaryVersion | null;
  latestDecision: BoundaryDecision | null;
  revision: number;
}

export interface ImpactChange {
  field: string;
  changeType: string;
  previous: string | null;
  proposed: string | null;
}

export interface ImpactContribution {
  context: string;
  complete: boolean;
  records: { recordType: string; recordId: string; reason: string }[];
}

export interface ImpactPreview {
  revision: number;
  approvedVersionId: string | null;
  changes: ImpactChange[];
  contributions: ImpactContribution[];
  pendingContexts: string[];
  complete: boolean;
  digest: string;
}

export const emptyBoundaryContent: BoundaryContent = {
  statement: '',
  engagementStage: 'readiness',
  categories: ['security'],
  entries: [],
};

export function newScopeEntry(): ScopeEntry {
  return {
    entry_id: crypto.randomUUID(),
    kind: 'inclusion',
    subject_type: 'service',
    subject: '',
    governed_record_id: null,
    owner_reference: '',
    rationale: '',
    unresolved: true,
  };
}

type EntryData = ScopeEntry;
type ContentData = {
  statement: string;
  engagement_stage: string;
  trust_services_categories: (string | null)[];
  entries: (EntryData | null)[];
};
type VersionData = {
  version_id: string;
  revision: number | string;
  status: string;
  effective_from?: unknown;
  author_member_id: string;
  author_display: string;
  changed_at: string;
  content: ContentData;
};
type DecisionData = {
  decision_id: string;
  version_id: string;
  revision: number | string;
  outcome: string;
  actor_display: string;
  actor?: { display: string } | null;
  rationale: string;
  decided_at: string;
  impact_digest: string | null;
  separation_of_duties_waiver_id?: string | null;
};
type BoundaryData = {
  boundary_id: string;
  program_id: string;
  draft?: VersionData | null;
  latest_approved_version?: VersionData | null;
  latest_decision?: DecisionData | null;
  revision?: number | string;
};

function toContent(data: ContentData): BoundaryContent {
  return {
    statement: data.statement,
    engagementStage: data.engagement_stage,
    categories: data.trust_services_categories.filter((value) => value !== null),
    entries: data.entries.filter((value) => value !== null),
  };
}

function toBody(content: BoundaryContent) {
  return {
    statement: content.statement.trim(),
    engagement_stage: content.engagementStage,
    trust_services_categories: content.categories,
    entries: content.entries.map((entry) => ({
      ...entry,
      subject: entry.subject.trim(),
      owner_reference: entry.owner_reference.trim(),
      rationale: entry.rationale.trim(),
      governed_record_id: entry.unresolved ? null : entry.governed_record_id?.trim() || null,
    })),
  };
}

function toVersion(data: VersionData): BoundaryVersion {
  return {
    versionId: data.version_id,
    revision: Number(data.revision),
    status: data.status,
    effectiveFrom: typeof data.effective_from === 'string' ? data.effective_from : null,
    author: data.author_display,
    authorMemberId: data.author_member_id,
    changedAt: data.changed_at,
    content: toContent(data.content),
  };
}

function toDecision(data: DecisionData): BoundaryDecision {
  return {
    decisionId: data.decision_id,
    versionId: data.version_id,
    revision: Number(data.revision),
    outcome: data.outcome,
    actor: data.actor?.display ?? data.actor_display,
    rationale: data.rationale,
    decidedAt: data.decided_at,
    impactDigest: data.impact_digest,
    waiverId: data.separation_of_duties_waiver_id ?? null,
  };
}

function toBoundary(data: BoundaryData): Boundary {
  const draft = data.draft ? toVersion(data.draft) : null;
  return {
    boundaryId: data.boundary_id,
    programId: data.program_id,
    draft,
    approved: data.latest_approved_version ? toVersion(data.latest_approved_version) : null,
    latestDecision: data.latest_decision ? toDecision(data.latest_decision) : null,
    revision: data.revision === undefined ? (draft?.revision ?? 0) : Number(data.revision),
  };
}

export async function listBoundaries(programId: string): Promise<Boundary[]> {
  const tenantId = requireActiveTenantId();
  const boundaries: Boundary[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listProgramBoundaries({
      params: { tenant_id: tenantId, program_id: programId },
      query: { cursor },
    });
    if (!result.ok) throw programFailure(result, 'load the boundaries');
    for (const item of result.data?.items ?? []) {
      if (item) boundaries.push(toBoundary(item));
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return boundaries;
}

// `minimumRevision` asks the server to wait for (or report lag behind) a revision this client just wrote.
export async function getBoundary(boundaryId: string, minimumRevision?: number): Promise<Boundary> {
  const tenantId = requireActiveTenantId();
  const result = await client.getBoundary({
    params: { tenant_id: tenantId, boundary_id: boundaryId },
    query: { minimum_revision: minimumRevision },
  });
  if (!result.ok) throw programFailure(result, 'load this boundary');
  if (!result.data) throw new ProgramRequestError('This boundary was not found.', 404, false);
  return toBoundary(result.data);
}

export async function createBoundary(programId: string, content: BoundaryContent): Promise<string> {
  const tenantId = requireActiveTenantId();
  const result = await client.createBoundary({
    params: { tenant_id: tenantId, program_id: programId },
    body: { content: toBody(content) },
  });
  if (!result.ok) throw programFailure(result, 'create the boundary');
  if (!result.data) throw new ProgramRequestError('The boundary was not created.', null, false);
  return result.data.boundary_id;
}

export async function reviseBoundaryDraft(
  boundaryId: string,
  draftVersionId: string,
  expectedRevision: number,
  content: BoundaryContent
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.reviseBoundaryDraft({
    params: { tenant_id: tenantId, boundary_id: boundaryId, draft_version_id: draftVersionId },
    body: { expected_revision: expectedRevision, content: toBody(content) },
  });
  if (!result.ok) throw programFailure(result, 'save the boundary draft');
}

export async function discardBoundaryDraft(
  boundaryId: string,
  draftVersionId: string,
  expectedRevision: number,
  rationale: string
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.discardBoundaryDraft({
    params: { tenant_id: tenantId, boundary_id: boundaryId, draft_version_id: draftVersionId },
    body: { expected_revision: expectedRevision, rationale: rationale.trim() },
  });
  if (!result.ok) throw programFailure(result, 'discard the boundary draft');
}

export async function reviewBoundary(
  boundaryId: string,
  draftVersionId: string,
  expectedRevision: number,
  outcome: 'accept' | 'request_changes',
  rationale: string,
  waiverId?: string
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.reviewBoundary({
    params: { tenant_id: tenantId, boundary_id: boundaryId, draft_version_id: draftVersionId },
    body: {
      expected_revision: expectedRevision,
      outcome,
      rationale: rationale.trim(),
      ...(waiverId ? { separation_of_duties_waiver_id: waiverId } : {}),
    },
  });
  if (!result.ok) throw programFailure(result, 'review this boundary');
}

export async function approveBoundary(
  boundaryId: string,
  draftVersionId: string,
  approval: {
    expectedRevision: number;
    acceptedReviewDecisionId: string;
    effectiveFrom: string;
    rationale: string;
    impactDigest: string;
    waiverId?: string;
  }
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.approveBoundary({
    params: { tenant_id: tenantId, boundary_id: boundaryId, draft_version_id: draftVersionId },
    body: {
      expected_revision: approval.expectedRevision,
      accepted_review_decision_id: approval.acceptedReviewDecisionId,
      effective_from: approval.effectiveFrom,
      rationale: approval.rationale.trim(),
      impact_digest: approval.impactDigest,
      ...(approval.waiverId ? { separation_of_duties_waiver_id: approval.waiverId } : {}),
    },
  });
  if (!result.ok) throw programFailure(result, 'approve this boundary');
}

export async function previewBoundaryImpact(
  boundaryId: string,
  draftVersionId: string,
  expectedRevision: number
): Promise<ImpactPreview> {
  const tenantId = requireActiveTenantId();
  const result = await client.previewBoundaryImpact({
    params: { tenant_id: tenantId, boundary_id: boundaryId, draft_version_id: draftVersionId },
    query: { expected_revision: expectedRevision },
  });
  if (!result.ok) throw programFailure(result, 'preview the boundary impact');
  const data = result.data;
  if (!data) throw new ProgramRequestError('The impact preview is not available.', 404, false);
  return {
    revision: Number(data.revision),
    approvedVersionId: data.approved_version_id,
    changes: (data.changes ?? [])
      .filter((change) => change !== null)
      .map((change) => ({
        field: change.field,
        changeType: change.change_type,
        previous: change.previous_entry?.subject ?? change.previous_value,
        proposed: change.proposed_entry?.subject ?? change.proposed_value,
      })),
    contributions: (data.contributions ?? [])
      .filter((item) => item !== null)
      .map((item) => ({
        context: item.context,
        complete: item.complete,
        records: (item.records ?? [])
          .filter((record) => record !== null)
          .map((record) => ({ recordType: record.record_type, recordId: record.record_id, reason: record.reason })),
      })),
    pendingContexts: (data.pending_contexts ?? []).filter((value) => value !== null),
    complete: data.complete,
    digest: data.digest,
  };
}

export async function proposeBoundarySuccessor(
  boundaryId: string,
  approvedVersionId: string,
  content: BoundaryContent
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.proposeBoundarySuccessor({
    params: { tenant_id: tenantId, boundary_id: boundaryId },
    body: { expected_approved_version_id: approvedVersionId, content: toBody(content) },
  });
  if (!result.ok) throw programFailure(result, 'propose a successor draft');
}

export async function listBoundaryVersions(boundaryId: string): Promise<BoundaryVersion[]> {
  const tenantId = requireActiveTenantId();
  const versions: BoundaryVersion[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listBoundaryVersions({
      params: { tenant_id: tenantId, boundary_id: boundaryId },
      query: { cursor },
    });
    if (!result.ok) throw programFailure(result, 'load the boundary versions');
    for (const item of result.data?.items ?? []) {
      if (item) versions.push(toVersion(item));
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return versions.sort((a, b) => b.revision - a.revision);
}

// Returns null when no approved version is effective on the given date.
export async function getEffectiveBoundaryVersion(boundaryId: string, effectiveOn: string): Promise<BoundaryVersion | null> {
  const tenantId = requireActiveTenantId();
  const result = await client.getEffectiveBoundaryVersion({
    params: { tenant_id: tenantId, boundary_id: boundaryId },
    query: { effective_on: effectiveOn },
  });
  if (!result.ok) {
    if (result.status === 404) return null;
    throw programFailure(result, 'load the effective boundary version');
  }
  return result.data ? toVersion(result.data) : null;
}

export async function listBoundaryDecisions(boundaryId: string): Promise<BoundaryDecision[]> {
  const tenantId = requireActiveTenantId();
  const decisions: BoundaryDecision[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listBoundaryDecisions({
      params: { tenant_id: tenantId, boundary_id: boundaryId },
      query: { cursor },
    });
    if (!result.ok) throw programFailure(result, 'load the boundary decisions');
    for (const item of result.data?.items ?? []) {
      if (item) decisions.push(toDecision(item));
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return decisions.sort((a, b) => b.decidedAt.localeCompare(a.decidedAt));
}

// The latest accepted review of the given draft revision, which approval must cite.
export function acceptedReviewFor(decisions: BoundaryDecision[], draft: BoundaryVersion): BoundaryDecision | null {
  const latest = decisions.find(
    (decision) =>
      decision.versionId === draft.versionId &&
      decision.revision === draft.revision &&
      (decision.outcome === 'accept' || decision.outcome === 'request_changes')
  );
  return latest?.outcome === 'accept' ? latest : null;
}

export { ProgramRequestError };
