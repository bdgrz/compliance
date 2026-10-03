import { createApiClient } from '../../api-client/index.js';
import { programFailure, ProgramRequestError } from '../programs/programs.js';
import { requireActiveTenantId } from '../tenants/tenants.js';

const client = createApiClient();

export interface MappingVersion {
  versionNumber: number;
  controlVersionId: string;
  status: string;
  rationale: string;
  applicabilityExplanation: string;
  proposedBy: string;
  proposedAt: string;
  reviewedBy: string | null;
  reviewRationale: string | null;
  reviewedAt: string | null;
  waived: boolean;
  retiredBy: string | null;
  retirementRationale: string | null;
  retiredAt: string | null;
}

export interface ControlMapping {
  mappingId: string;
  controlId: string;
  editionId: string;
  criterionIdentifier: string;
  criterionKind: string;
  revision: number;
  status: string;
  activeVersionNumber: number | null;
  activeControlVersionId: string | null;
  versions: MappingVersion[];
}

export interface CoverageRow {
  identifier: string;
  kind: string;
  category: string;
  parentIdentifier: string | null;
  summary: string;
  coverageState: 'mapped' | 'unmapped' | 'not_applicable';
  notApplicableDecisionId: string | null;
  mappedControls: {
    mappingId: string;
    controlId: string;
    controlVersionId: string;
    versionNumber: number;
    applicabilityExplanation: string;
    remapRequired: boolean;
  }[];
  pendingProposalCount: number;
}

export interface ApplicabilityVersion {
  versionNumber: number;
  status: string;
  rationale: string;
  proposedBy: string;
  proposedAt: string;
  reviewedBy: string | null;
  reviewRationale: string | null;
  reviewedAt: string | null;
}

export interface ApplicabilityDecision {
  decisionId: string;
  criterionIdentifier: string;
  revision: number;
  status: string;
  activeVersionNumber: number | null;
  versions: ApplicabilityVersion[];
}

export function pendingApplicability(
  decision: ApplicabilityDecision
): ApplicabilityVersion | null {
  return (
    decision.versions.find((version) => version.status === 'pending') ?? null
  );
}

export function activeApplicability(
  decision: ApplicabilityDecision
): ApplicabilityVersion | null {
  return (
    decision.versions.find(
      (version) => version.versionNumber === decision.activeVersionNumber
    ) ?? null
  );
}

type Actor = { display: string } | null | undefined;

type MappingData = {
  mapping_id: string;
  control_id: string;
  edition_id: string;
  criterion_identifier: string;
  criterion_kind: string;
  revision: number | string;
  status: string;
  active_version_number: number | string | null;
  active_control_version_id: string | null;
  versions: ({
    version_number: number | string;
    control_version_id: string;
    status: string;
    rationale: string;
    applicability_explanation: string;
    proposed_by: Actor;
    proposed_at: string;
    reviewed_by: Actor;
    review_rationale: string | null;
    reviewed_at: string | null;
    separation_of_duties_waiver_id: string | null;
    retired_by: Actor;
    retirement_rationale: string | null;
    retired_at: string | null;
  } | null)[];
};

function toMapping(data: MappingData): ControlMapping {
  return {
    mappingId: data.mapping_id,
    controlId: data.control_id,
    editionId: data.edition_id,
    criterionIdentifier: data.criterion_identifier,
    criterionKind: data.criterion_kind,
    revision: Number(data.revision),
    status: data.status,
    activeVersionNumber:
      data.active_version_number === null
        ? null
        : Number(data.active_version_number),
    activeControlVersionId: data.active_control_version_id,
    versions: data.versions
      .filter((version) => version !== null)
      .map((version) => ({
        versionNumber: Number(version.version_number),
        controlVersionId: version.control_version_id,
        status: version.status,
        rationale: version.rationale,
        applicabilityExplanation: version.applicability_explanation,
        proposedBy: version.proposed_by?.display ?? 'Unknown',
        proposedAt: version.proposed_at,
        reviewedBy: version.reviewed_at
          ? (version.reviewed_by?.display ?? null)
          : null,
        reviewRationale: version.review_rationale,
        reviewedAt: version.reviewed_at,
        waived: version.separation_of_duties_waiver_id !== null,
        retiredBy: version.retired_at
          ? (version.retired_by?.display ?? null)
          : null,
        retirementRationale: version.retirement_rationale,
        retiredAt: version.retired_at,
      }))
      .sort((a, b) => b.versionNumber - a.versionNumber),
  };
}

export function pendingVersion(mapping: ControlMapping): MappingVersion | null {
  return (
    mapping.versions.find((version) => version.status === 'pending') ?? null
  );
}

export async function listControlMappings(
  programId: string,
  controlId: string,
  editionId: string
): Promise<ControlMapping[]> {
  const tenantId = requireActiveTenantId();
  const mappings: ControlMapping[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listControlCriterionMappings({
      params: { tenant_id: tenantId, program_id: programId },
      query: { control_id: controlId, edition_id: editionId, cursor },
    });
    if (!result.ok) throw programFailure(result, 'load the criteria mappings');
    for (const item of result.data?.items ?? []) {
      if (item) mappings.push(toMapping(item));
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return mappings.sort((a, b) =>
    a.criterionIdentifier.localeCompare(b.criterionIdentifier)
  );
}

export interface MappingProposal {
  controlId: string;
  controlVersionId: string;
  editionId: string;
  criterionIdentifier: string;
  expectedRevision: number;
  rationale: string;
  applicabilityExplanation: string;
}

export async function proposeControlMapping(
  programId: string,
  proposal: MappingProposal
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.proposeControlCriterionMapping({
    params: { tenant_id: tenantId, program_id: programId },
    body: {
      control_id: proposal.controlId,
      control_version_id: proposal.controlVersionId,
      edition_id: proposal.editionId,
      criterion_identifier: proposal.criterionIdentifier,
      expected_revision: proposal.expectedRevision,
      rationale: proposal.rationale.trim(),
      applicability_explanation: proposal.applicabilityExplanation.trim(),
    },
  });
  if (!result.ok) throw programFailure(result, 'propose the criteria mapping');
}

export async function reviewControlMapping(
  programId: string,
  mappingId: string,
  expectedRevision: number,
  outcome: 'accept' | 'reject',
  rationale: string,
  waiverId: string
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.reviewControlCriterionMapping({
    params: {
      tenant_id: tenantId,
      program_id: programId,
      mapping_id: mappingId,
    },
    body: {
      expected_revision: expectedRevision,
      outcome,
      rationale: rationale.trim(),
      ...(waiverId.trim()
        ? { separation_of_duties_waiver_id: waiverId.trim() }
        : {}),
    },
  });
  if (!result.ok) throw programFailure(result, 'review the criteria mapping');
}

export async function retireControlMapping(
  programId: string,
  mappingId: string,
  expectedRevision: number,
  rationale: string
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.retireControlCriterionMapping({
    params: {
      tenant_id: tenantId,
      program_id: programId,
      mapping_id: mappingId,
    },
    body: { expected_revision: expectedRevision, rationale: rationale.trim() },
  });
  if (!result.ok) throw programFailure(result, 'retire the criteria mapping');
}

export async function listCriteriaCoverage(
  programId: string,
  editionId: string,
  coverageState?: 'mapped' | 'unmapped'
): Promise<CoverageRow[]> {
  const tenantId = requireActiveTenantId();
  const rows: CoverageRow[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listCriteriaCoverage({
      params: { tenant_id: tenantId, program_id: programId },
      query: { edition_id: editionId, coverage_state: coverageState, cursor },
    });
    if (!result.ok) throw programFailure(result, 'load the criteria coverage');
    for (const item of result.data?.items ?? []) {
      if (item) {
        rows.push({
          identifier: item.identifier,
          kind: item.kind,
          category: item.category,
          parentIdentifier: item.parent_identifier,
          summary: item.summary,
          coverageState:
            item.coverage_state === 'mapped' ||
            item.coverage_state === 'not_applicable'
              ? item.coverage_state
              : 'unmapped',
          notApplicableDecisionId: item.not_applicable_decision_id ?? null,
          mappedControls: item.mapped_controls
            .filter((control) => control !== null)
            .map((control) => ({
              mappingId: control.mapping_id,
              controlId: control.control_id,
              controlVersionId: control.control_version_id,
              versionNumber: Number(control.version_number),
              applicabilityExplanation: control.applicability_explanation,
              remapRequired: control.remap_required === true,
            })),
          pendingProposalCount: Number(item.pending_proposal_count),
        });
      }
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return rows;
}

export async function listCriterionApplicability(
  programId: string,
  editionId: string
): Promise<ApplicabilityDecision[]> {
  const tenantId = requireActiveTenantId();
  const decisions: ApplicabilityDecision[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listCriterionApplicability({
      params: { tenant_id: tenantId, program_id: programId },
      query: { edition_id: editionId, cursor },
    });
    if (!result.ok)
      throw programFailure(result, 'load the applicability decisions');
    for (const item of result.data?.items ?? []) {
      if (!item) continue;
      decisions.push({
        decisionId: item.decision_id,
        criterionIdentifier: item.criterion_identifier,
        revision: Number(item.revision),
        status: item.status,
        activeVersionNumber:
          item.active_version_number === null
            ? null
            : Number(item.active_version_number),
        versions: item.versions
          .filter((version) => version !== null)
          .map((version) => ({
            versionNumber: Number(version.version_number),
            status: version.status,
            rationale: version.rationale,
            proposedBy: version.proposed_by.display,
            proposedAt: version.proposed_at,
            reviewedBy: version.reviewed_by?.display ?? null,
            reviewRationale: version.review_rationale,
            reviewedAt: version.reviewed_at,
          }))
          .sort((a, b) => b.versionNumber - a.versionNumber),
      });
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return decisions;
}

export async function proposeCriterionNotApplicable(
  programId: string,
  editionId: string,
  criterionIdentifier: string,
  expectedRevision: number,
  rationale: string
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.proposeCriterionNotApplicable({
    params: { tenant_id: tenantId, program_id: programId },
    body: {
      edition_id: editionId,
      criterion_identifier: criterionIdentifier,
      expected_revision: expectedRevision,
      rationale: rationale.trim(),
    },
  });
  if (!result.ok)
    throw programFailure(result, 'propose the not-applicable decision');
}

export async function reviewCriterionApplicability(
  programId: string,
  decisionId: string,
  expectedRevision: number,
  outcome: 'accept' | 'reject',
  rationale: string,
  waiverId: string
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.reviewCriterionApplicability({
    params: {
      tenant_id: tenantId,
      program_id: programId,
      decision_id: decisionId,
    },
    body: {
      expected_revision: expectedRevision,
      outcome,
      rationale: rationale.trim(),
      ...(waiverId.trim()
        ? { separation_of_duties_waiver_id: waiverId.trim() }
        : {}),
    },
  });
  if (!result.ok)
    throw programFailure(result, 'review the not-applicable decision');
}

export async function withdrawCriterionNotApplicable(
  programId: string,
  decisionId: string,
  expectedRevision: number,
  rationale: string
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.withdrawCriterionNotApplicable({
    params: {
      tenant_id: tenantId,
      program_id: programId,
      decision_id: decisionId,
    },
    body: { expected_revision: expectedRevision, rationale: rationale.trim() },
  });
  if (!result.ok)
    throw programFailure(result, 'withdraw the not-applicable decision');
}

export function describeMappingFailure(error: Error): string {
  if (error instanceof ProgramRequestError && error.status === 403)
    return error.message;
  if (
    error instanceof ProgramRequestError &&
    error.status === 409 &&
    !error.transient &&
    isStaleConflict(error)
  ) {
    return 'Someone else changed this mapping since you opened it. Reload to see their changes, then try again.';
  }
  return error.message;
}

// A stale optimistic revision (reload and retry) versus a state conflict whose detail explains itself.
export function isStaleConflict(error: Error): boolean {
  return (
    error instanceof ProgramRequestError &&
    error.status === 409 &&
    !error.transient &&
    /Current (revision|draft version|approved version):/.test(error.message)
  );
}

export { ProgramRequestError };
