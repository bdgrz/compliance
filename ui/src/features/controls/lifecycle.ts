import { createApiClient } from '../../api-client/index.js';
import { programFailure, ProgramRequestError } from '../programs/programs.js';
import { requireActiveTenantId } from '../tenants/tenants.js';
import { isStaleConflict } from '../control-mappings/mappings.js';
import type { ApplicabilityReference, ControlContent } from './controls.js';

const client = createApiClient();

export interface ControlVersion {
  versionId: string;
  revision: number;
  status: string;
  content: ControlContent;
  effectiveFrom: string;
  effectiveUntil: string | null;
  predecessorVersionId: string | null;
  ownerResolution: string;
  approvedBy: string;
  approvalRationale: string;
  approvedAt: string;
  waived: boolean;
}

export interface ControlDecision {
  decisionId: string;
  versionId: string;
  revision: number;
  kind: string;
  outcome: string;
  actor: string;
  rationale: string;
  decidedAt: string;
  waived: boolean;
}

export interface ImpactChange {
  field: string;
  changeType: string;
  previousValue: string | null;
  proposedValue: string | null;
}

export interface ImpactContribution {
  context: string;
  status: string;
  freshness: string;
  complete: boolean;
  records: { recordType: string; recordId: string; reason: string }[];
}

export interface ImpactPreview {
  kind: 'successor' | 'retirement';
  targetId: string;
  revision: number;
  changes: ImpactChange[];
  contributions: ImpactContribution[];
  pendingContexts: string[];
  complete: boolean;
  digest: string;
}

export interface DecisionInput {
  rationale: string;
  waiverId: string;
}

type ContentData = {
  title: string;
  objective: string;
  description: string;
  implementation_narrative: string;
  expected_evidence_descriptions: (string | null)[];
  owner_reference?: string | null;
  applicability?: (ApplicabilityReference | null)[] | null;
};

type VersionData = {
  version_id: string;
  revision: number | string;
  status: string;
  content: ContentData;
  effective_from: string;
  effective_until?: string | null;
  predecessor_version_id: string | null;
  owner_resolution: string;
  approved_by: { display: string };
  approval_rationale: string;
  approved_at: string;
  separation_of_duties_waiver_id: string | null;
};

function toContent(data: ContentData): ControlContent {
  return {
    title: data.title,
    objective: data.objective,
    description: data.description,
    implementationNarrative: data.implementation_narrative,
    expectedEvidence: data.expected_evidence_descriptions.filter(
      (value) => value !== null
    ),
    ownerReference: data.owner_reference ?? null,
    applicability: (data.applicability ?? []).filter((value) => value !== null),
  };
}

function toVersion(data: VersionData): ControlVersion {
  return {
    versionId: data.version_id,
    revision: Number(data.revision),
    status: data.status,
    content: toContent(data.content),
    effectiveFrom: data.effective_from,
    effectiveUntil: data.effective_until ?? null,
    predecessorVersionId: data.predecessor_version_id,
    ownerResolution: data.owner_resolution,
    approvedBy: data.approved_by.display,
    approvalRationale: data.approval_rationale,
    approvedAt: data.approved_at,
    waived: data.separation_of_duties_waiver_id !== null,
  };
}

function ids(tenantId: string, programId: string, controlId: string) {
  return { tenant_id: tenantId, program_id: programId, control_id: controlId };
}

function waiver(input: DecisionInput) {
  const id = input.waiverId.trim();
  return id ? { separation_of_duties_waiver_id: id } : {};
}

// Null means the control has never been approved; any other failure is surfaced.
export async function getCurrentControlVersion(
  programId: string,
  controlId: string
): Promise<ControlVersion | null> {
  const tenantId = requireActiveTenantId();
  const result = await client.getCurrentControlVersion({
    params: ids(tenantId, programId, controlId),
  });
  if (!result.ok) {
    if (result.status === 404) return null;
    throw programFailure(result, 'load the approved control version');
  }
  if (!result.data) return null;
  return toVersion(result.data);
}

export async function getEffectiveControlVersion(
  programId: string,
  controlId: string,
  effectiveOn: string
): Promise<ControlVersion | null> {
  const tenantId = requireActiveTenantId();
  const result = await client.getEffectiveControlVersion({
    params: ids(tenantId, programId, controlId),
    query: { effective_on: effectiveOn },
  });
  if (!result.ok) {
    if (result.status === 404) return null;
    throw programFailure(result, 'load the effective control version');
  }
  return result.data ? toVersion(result.data) : null;
}

export async function listControlVersions(
  programId: string,
  controlId: string
): Promise<ControlVersion[]> {
  const tenantId = requireActiveTenantId();
  const versions: ControlVersion[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listControlVersions({
      params: ids(tenantId, programId, controlId),
      query: { cursor },
    });
    if (!result.ok) throw programFailure(result, 'load the control versions');
    for (const item of result.data?.items ?? []) {
      if (item) versions.push(toVersion(item));
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return versions.sort(
    (a, b) =>
      b.effectiveFrom.localeCompare(a.effectiveFrom) || b.revision - a.revision
  );
}

export async function listControlDecisions(
  programId: string,
  controlId: string
): Promise<ControlDecision[]> {
  const tenantId = requireActiveTenantId();
  const decisions: ControlDecision[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listControlDecisions({
      params: ids(tenantId, programId, controlId),
      query: { cursor },
    });
    if (!result.ok) throw programFailure(result, 'load the control decisions');
    for (const item of result.data?.items ?? []) {
      if (item) {
        decisions.push({
          decisionId: item.decision_id,
          versionId: item.version_id,
          revision: Number(item.revision),
          kind: item.kind,
          outcome: item.outcome,
          actor: item.actor.display,
          rationale: item.rationale,
          decidedAt: item.decided_at,
          waived:
            item.separation_of_duties_waiver_id !== null ||
            item.separation_of_duties_waived === true,
        });
      }
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return decisions.sort((a, b) => b.decidedAt.localeCompare(a.decidedAt));
}

// The approval or retirement must cite the latest review of the exact pending revision, and only
// when that review accepted it. `targetId` narrows to the pending successor or retirement.
export function acceptedReview(
  decisions: ControlDecision[],
  revision: number,
  targetId: string | null
): ControlDecision | null {
  const latest = decisions.find(
    (decision) =>
      decision.kind === 'review' &&
      decision.revision === revision &&
      (targetId === null || decision.versionId === targetId)
  );
  return latest?.outcome === 'accept' ? latest : null;
}

// Null means nothing is pending on an approved control (the server answers a non-transient 409).
export async function previewControlImpact(
  programId: string,
  controlId: string,
  expectedRevision: number
): Promise<ImpactPreview | null> {
  const tenantId = requireActiveTenantId();
  const result = await client.previewControlImpact({
    params: ids(tenantId, programId, controlId),
    query: { expected_revision: expectedRevision },
  });
  if (!result.ok) {
    const failure = programFailure(result, 'preview the control impact');
    if (
      failure.status === 409 &&
      !failure.transient &&
      failure.message.startsWith('Impact preview requires')
    )
      return null;
    throw failure;
  }
  const data = result.data;
  if (!data) return null;
  return {
    kind: data.kind === 'retirement' ? 'retirement' : 'successor',
    targetId: data.target_id,
    revision: Number(data.revision),
    changes: data.changes
      .filter((change) => change !== null)
      .map((change) => ({
        field: change.field,
        changeType: change.change_type,
        previousValue: change.previous_value,
        proposedValue: change.proposed_value,
      })),
    contributions: data.contributions
      .filter((contribution) => contribution !== null)
      .map((contribution) => ({
        context: contribution.context,
        status: contribution.status,
        freshness: contribution.freshness,
        complete: contribution.complete,
        records: contribution.records
          .filter((record) => record !== null)
          .map((record) => ({
            recordType: record.record_type,
            recordId: record.record_id,
            reason: record.reason,
          })),
      })),
    pendingContexts: data.pending_contexts.filter((value) => value !== null),
    complete: data.complete,
    digest: data.digest,
  };
}

export async function reviewControl(
  programId: string,
  controlId: string,
  expectedRevision: number,
  outcome: 'accept' | 'request_changes',
  input: DecisionInput
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.reviewControl({
    params: ids(tenantId, programId, controlId),
    body: {
      expected_revision: expectedRevision,
      outcome,
      rationale: input.rationale.trim(),
      ...waiver(input),
    },
  });
  if (!result.ok) throw programFailure(result, 'review this control');
}

export async function approveControl(
  programId: string,
  controlId: string,
  expectedRevision: number,
  acceptedReviewDecisionId: string,
  effectiveFrom: string,
  impactDigest: string | null,
  input: DecisionInput
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.approveControl({
    params: ids(tenantId, programId, controlId),
    body: {
      expected_revision: expectedRevision,
      accepted_review_decision_id: acceptedReviewDecisionId,
      effective_from: effectiveFrom,
      rationale: input.rationale.trim(),
      impact_digest: impactDigest,
      ...waiver(input),
    },
  });
  if (!result.ok) throw programFailure(result, 'approve this control');
}

export async function proposeControlSuccessor(
  programId: string,
  controlId: string,
  expectedApprovedVersionId: string,
  content: ControlContent
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.proposeControlSuccessor({
    params: ids(tenantId, programId, controlId),
    body: {
      expected_approved_version_id: expectedApprovedVersionId,
      content: {
        title: content.title.trim(),
        objective: content.objective.trim(),
        description: content.description.trim(),
        implementation_narrative: content.implementationNarrative.trim(),
        expected_evidence_descriptions: content.expectedEvidence,
        owner_reference: content.ownerReference?.trim()
          ? content.ownerReference.trim()
          : null,
        applicability: content.applicability,
      },
    },
  });
  if (!result.ok) throw programFailure(result, 'propose a successor version');
}

export async function proposeControlRetirement(
  programId: string,
  controlId: string,
  expectedApprovedVersionId: string,
  effectiveUntil: string,
  rationale: string
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.proposeControlRetirement({
    params: ids(tenantId, programId, controlId),
    body: {
      expected_approved_version_id: expectedApprovedVersionId,
      effective_until: effectiveUntil,
      rationale: rationale.trim(),
    },
  });
  if (!result.ok) throw programFailure(result, 'propose retiring this control');
}

export async function retireControl(
  programId: string,
  controlId: string,
  expectedRevision: number,
  acceptedReviewDecisionId: string,
  impactDigest: string,
  input: DecisionInput
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.retireControl({
    params: ids(tenantId, programId, controlId),
    body: {
      expected_revision: expectedRevision,
      accepted_review_decision_id: acceptedReviewDecisionId,
      impact_digest: impactDigest,
      rationale: input.rationale.trim(),
      ...waiver(input),
    },
  });
  if (!result.ok) throw programFailure(result, 'retire this control');
}

export function describeDecisionFailure(error: Error): string {
  if (isStaleConflict(error)) {
    return 'Someone else changed this control since you opened it. Reload to see their changes, then decide again.';
  }
  return error.message;
}

export { ProgramRequestError };
