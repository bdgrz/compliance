import { createApiClient } from '../../api-client/index.js';
import { requireActiveTenantId } from '../tenants/tenants.js';

const client = createApiClient();

export type AssessmentPhase = 'inherent' | 'target' | 'residual';
export type TreatmentKind = 'mitigate' | 'accept' | 'transfer' | 'avoid';
export type ApproverAuthority = 'compliance_lead' | 'executive';

export interface RiskDraft {
  riskId: string;
  identifier: string;
  revision: number;
  status: string;
  title: string;
  scenario: string;
  potentialEffect: string;
  sourceNote: string | null;
  lastChangedBy: string;
  lastChangedAt: string;
}

export interface RiskDraftRevision {
  revision: number;
  title: string;
  scenario: string;
  potentialEffect: string;
  sourceNote: string | null;
  changedBy: string;
  changedAt: string;
}

export interface RiskControlTreatment {
  treatmentId: string;
  status: string;
  rationale: string;
  proposedBy: string;
  proposedAt: string;
  reviewedBy: string | null;
  reviewRationale: string | null;
}

export interface RiskReassessmentTrigger {
  triggerId: string;
  kind: string;
  sourceReference: string;
  raisedAt: string;
  status: string | null;
}

export interface RiskGovernance {
  revision: number;
  ownerPersonId: string | null;
  ownerRationale: string | null;
  controlTreatments: RiskControlTreatment[];
  reassessmentTriggers: RiskReassessmentTrigger[];
}

export type TreatmentReviewOutcome = 'accept' | 'reject';

export interface RiskMethod {
  version: number;
  likelihoodScale: string[];
  impactScale: string[];
  appetiteThreshold: number | null;
  reassessmentInterval: string;
  publishedBy: string;
  publishedAt: string;
}

export interface RiskAssessment {
  assessmentId: string;
  phase: string;
  methodVersion: number;
  likelihood: number;
  impact: number;
  score: number;
  rationale: string;
  assessor: string;
  assessedAt: string;
}

export interface RiskTreatment {
  kind: string;
  rationale: string;
  chosenBy: string;
  chosenAt: string;
}

export interface RiskAcceptance {
  acceptanceId: string;
  residualAssessmentId: string;
  residualScore: number;
  appetiteThreshold: number | null;
  approver: string;
  approverAuthority: string;
  rationale: string;
  acceptedAt: string;
  expiresAt: string;
  status: string | null;
}

export interface RiskEvaluation {
  revision: number;
  status: string;
  assessments: RiskAssessment[];
  treatment: RiskTreatment | null;
  acceptances: RiskAcceptance[];
  reassessmentDueAt: string | null;
}

export interface RiskEvaluationEvent {
  revision: number;
  kind: string;
  actor: string;
  occurredAt: string;
  summary: string;
}

// Keeps the HTTP status and the server's transient flag so pages can tell forbidden, conflict,
// and projection lag (a transient 409) apart.
export class RiskRequestError extends Error {
  constructor(
    message: string,
    readonly status: number | null,
    readonly transient: boolean
  ) {
    super(message);
  }
}

const statusLabels: Record<string, string> = {
  unassessed: 'Not yet assessed',
  assessed: 'Assessed',
  treatment_chosen: 'Treatment chosen',
  residual_assessed: 'Residual risk assessed',
  acceptance_pending: 'Acceptance pending',
  accepted: 'Accepted',
  reassessment_due: 'Reassessment due',
};

export function riskStatusLabel(status: string): string {
  return statusLabels[status] ?? status.replaceAll('_', ' ');
}

const triggerLabels: Record<string, string> = {
  method_changed: 'Risk method changed',
  boundary_changed: 'Boundary changed',
};

export function triggerLabel(kind: string): string {
  return triggerLabels[kind] ?? kind.replaceAll('_', ' ');
}

export const phaseLabels: Record<AssessmentPhase, string> = {
  inherent: 'Inherent',
  target: 'Target',
  residual: 'Residual',
};

export const treatmentLabels: Record<TreatmentKind, string> = {
  mitigate: 'Mitigate',
  accept: 'Accept',
  transfer: 'Transfer',
  avoid: 'Avoid',
};

export const authorityLabels: Record<ApproverAuthority, string> = {
  compliance_lead: 'Compliance lead',
  executive: 'Executive',
};

function toNumber(value: number | string | null | undefined): number | null {
  return value === null || value === undefined ? null : Number(value);
}

export async function listRiskDrafts(programId: string): Promise<RiskDraft[]> {
  const tenantId = requireActiveTenantId();
  const drafts: RiskDraft[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listRiskDrafts({
      params: { tenant_id: tenantId, program_id: programId },
      query: { cursor },
    });
    if (!result.ok) throw failure(result, 'load the risks');
    for (const item of result.data?.items ?? []) {
      if (item) {
        drafts.push({
          riskId: item.risk_id,
          identifier: item.identifier,
          revision: Number(item.revision),
          status: item.status,
          title: item.content.title,
          scenario: item.content.scenario,
          potentialEffect: item.content.potential_effect,
          sourceNote: item.content.source_note,
          lastChangedBy: item.last_changed_by?.display ?? item.last_changed_by_display,
          lastChangedAt: item.last_changed_at,
        });
      }
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return drafts.sort((a, b) => a.identifier.localeCompare(b.identifier));
}

export async function createRiskDraft(
  programId: string,
  draft: { identifier: string; title: string; scenario: string; potentialEffect: string }
): Promise<string> {
  const tenantId = requireActiveTenantId();
  const result = await client.createRiskDraft({
    params: { tenant_id: tenantId, program_id: programId },
    body: {
      identifier: draft.identifier,
      title: draft.title,
      scenario: draft.scenario,
      potential_effect: draft.potentialEffect,
    },
  });
  if (!result.ok) throw failure(result, 'record the risk');
  if (!result.data) throw new RiskRequestError('The risk was not recorded.', null, false);
  return result.data.risk_id;
}

export async function reviseRiskDraft(
  programId: string,
  riskId: string,
  expectedRevision: number,
  content: { title: string; scenario: string; potentialEffect: string; sourceNote: string | null }
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.reviseRiskDraft({
    params: { tenant_id: tenantId, program_id: programId, risk_id: riskId },
    body: {
      expected_revision: expectedRevision,
      title: content.title,
      scenario: content.scenario,
      potential_effect: content.potentialEffect,
      source_note: content.sourceNote,
    },
  });
  if (!result.ok) throw failure(result, 'save the risk');
}

export async function listRiskDraftRevisions(programId: string, riskId: string): Promise<RiskDraftRevision[]> {
  const tenantId = requireActiveTenantId();
  const revisions: RiskDraftRevision[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listRiskDraftRevisions({
      params: { tenant_id: tenantId, program_id: programId, risk_id: riskId },
      query: { cursor },
    });
    if (!result.ok) throw failure(result, 'load the risk draft history');
    for (const item of result.data?.items ?? []) {
      if (item) {
        revisions.push({
          revision: Number(item.revision),
          title: item.content.title,
          scenario: item.content.scenario,
          potentialEffect: item.content.potential_effect,
          sourceNote: item.content.source_note,
          changedBy: item.actor?.display ?? item.changed_by_display,
          changedAt: item.changed_at,
        });
      }
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return revisions.sort((a, b) => b.revision - a.revision);
}

export async function getRiskGovernance(programId: string, riskId: string): Promise<RiskGovernance> {
  const tenantId = requireActiveTenantId();
  const result = await client.getRiskGovernance({ params: { tenant_id: tenantId, program_id: programId, risk_id: riskId } });
  if (!result.ok) throw failure(result, 'load the risk owner and treatments');
  const data = result.data;
  if (!data) throw new RiskRequestError('This risk was not found.', 404, false);
  return {
    revision: Number(data.revision),
    ownerPersonId: data.owner?.person_id ?? null,
    ownerRationale: data.owner?.rationale ?? null,
    controlTreatments: data.control_treatments
      .filter((t) => t !== null)
      .map((t) => ({
        treatmentId: t.treatment_id,
        status: t.status,
        rationale: t.rationale,
        proposedBy: t.proposed_by.display,
        proposedAt: t.proposed_at,
        reviewedBy: t.reviewed_by?.display ?? null,
        reviewRationale: t.review_rationale,
      })),
    reassessmentTriggers: data.reassessment_triggers
      .filter((t) => t !== null)
      .map((t) => ({
        triggerId: t.trigger_id,
        kind: t.trigger_kind,
        sourceReference: t.source_reference,
        raisedAt: t.raised_at,
        status: t.status ?? null,
      })),
  };
}

export async function assignRiskOwner(
  programId: string,
  riskId: string,
  expectedRevision: number,
  personId: string,
  rationale: string | null
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.assignRiskOwner({
    params: { tenant_id: tenantId, program_id: programId, risk_id: riskId },
    body: { expected_revision: expectedRevision, person_id: personId, rationale },
  });
  if (!result.ok) throw failure(result, 'assign the risk owner');
}

export async function reviewRiskControlTreatment(
  programId: string,
  riskId: string,
  treatmentId: string,
  expectedRevision: number,
  outcome: TreatmentReviewOutcome,
  rationale: string
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.reviewRiskControlTreatment({
    params: { tenant_id: tenantId, program_id: programId, risk_id: riskId, treatment_id: treatmentId },
    body: { expected_revision: expectedRevision, outcome, rationale },
  });
  if (!result.ok) throw failure(result, 'review the control treatment');
}

// Returns null when the program has not published a risk method yet.
export async function getRiskMethod(programId: string): Promise<RiskMethod | null> {
  const tenantId = requireActiveTenantId();
  const result = await client.getRiskMethod({ params: { tenant_id: tenantId, program_id: programId } });
  if (!result.ok) {
    if (result.status === 404) return null;
    throw failure(result, 'load the risk method');
  }
  const data = result.data;
  if (!data) return null;
  return {
    version: Number(data.version),
    likelihoodScale: data.likelihood_scale.map((d) => d ?? ''),
    impactScale: data.impact_scale.map((d) => d ?? ''),
    appetiteThreshold: toNumber(data.appetite_threshold),
    reassessmentInterval: data.reassessment_interval,
    publishedBy: data.published_by.display,
    publishedAt: data.published_at,
  };
}

export async function publishRiskMethod(
  programId: string,
  expectedVersion: number,
  likelihoodScale: string[],
  impactScale: string[],
  appetiteThreshold: number | null
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.publishRiskMethodVersion({
    params: { tenant_id: tenantId, program_id: programId },
    body: {
      expected_version: expectedVersion,
      likelihood_scale: likelihoodScale,
      impact_scale: impactScale,
      ...(appetiteThreshold === null ? {} : { appetite_threshold: appetiteThreshold }),
    },
  });
  if (!result.ok) throw failure(result, 'publish the risk method');
}

export async function getRiskEvaluation(
  programId: string,
  riskId: string,
  minimumRevision?: number
): Promise<RiskEvaluation> {
  const tenantId = requireActiveTenantId();
  const result = await client.getRiskEvaluation({
    params: { tenant_id: tenantId, program_id: programId, risk_id: riskId },
    query: minimumRevision === undefined ? {} : { minimum_revision: minimumRevision },
  });
  if (!result.ok) throw failure(result, 'load the risk evaluation');
  const data = result.data;
  if (!data) throw new RiskRequestError('This risk was not found.', 404, false);
  return {
    revision: Number(data.revision),
    status: data.status,
    assessments: data.assessments.filter((a) => a !== null).map(toAssessment),
    treatment: data.treatment ? toTreatment(data.treatment) : null,
    acceptances: data.acceptances.filter((a) => a !== null).map(toAcceptance),
    reassessmentDueAt: data.reassessment_due_at,
  };
}

export async function listRiskEvaluationHistory(programId: string, riskId: string): Promise<RiskEvaluationEvent[]> {
  const tenantId = requireActiveTenantId();
  const events: RiskEvaluationEvent[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listRiskEvaluationHistory({
      params: { tenant_id: tenantId, program_id: programId, risk_id: riskId },
      query: { cursor },
    });
    if (!result.ok) throw failure(result, 'load the risk history');
    for (const item of result.data?.items ?? []) {
      if (!item) continue;
      const summary = item.assessment
        ? `${item.assessment.phase} assessment: likelihood ${item.assessment.likelihood}, impact ${item.assessment.impact}, score ${item.assessment.score}. ${item.assessment.rationale}`
        : item.treatment
          ? `Treatment ${item.treatment.kind}. ${item.treatment.rationale}`
          : item.acceptance
            ? `Accepted as ${item.acceptance.approver_authority.replaceAll('_', ' ')} until ${item.acceptance.expires_at.slice(0, 10)}. ${item.acceptance.rationale}`
            : item.kind.replaceAll('_', ' ');
      events.push({
        revision: Number(item.revision),
        kind: item.kind,
        actor: item.actor.display,
        occurredAt: item.occurred_at,
        summary,
      });
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return events.sort((a, b) => b.revision - a.revision);
}

export async function recordRiskAssessment(
  programId: string,
  riskId: string,
  expectedRevision: number,
  assessment: { methodVersion: number; phase: AssessmentPhase; likelihood: number; impact: number; rationale: string }
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.recordRiskAssessment({
    params: { tenant_id: tenantId, program_id: programId, risk_id: riskId },
    body: {
      expected_revision: expectedRevision,
      method_version: assessment.methodVersion,
      phase: assessment.phase,
      likelihood: assessment.likelihood,
      impact: assessment.impact,
      rationale: assessment.rationale,
    },
  });
  if (!result.ok) throw failure(result, 'record the assessment');
}

export async function chooseRiskTreatment(
  programId: string,
  riskId: string,
  expectedRevision: number,
  kind: TreatmentKind,
  rationale: string
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.chooseRiskTreatment({
    params: { tenant_id: tenantId, program_id: programId, risk_id: riskId },
    body: { expected_revision: expectedRevision, kind, rationale },
  });
  if (!result.ok) throw failure(result, 'choose the treatment');
}

export async function acceptRisk(
  programId: string,
  riskId: string,
  expectedRevision: number,
  acceptance: { residualAssessmentId: string; authority: ApproverAuthority; expiresAt: string; rationale: string }
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.acceptRisk({
    params: { tenant_id: tenantId, program_id: programId, risk_id: riskId },
    body: {
      expected_revision: expectedRevision,
      residual_assessment_id: acceptance.residualAssessmentId,
      approver_authority: acceptance.authority,
      expires_at: acceptance.expiresAt,
      rationale: acceptance.rationale,
    },
  });
  if (!result.ok) {
    if (result.status === 403) {
      throw new RiskRequestError(
        `You do not hold the ${authorityLabels[acceptance.authority].toLowerCase()} acceptance grant for this program.`,
        403,
        false
      );
    }
    throw failure(result, 'accept the risk');
  }
}

type AssessmentWire = {
  assessment_id: string;
  phase: string;
  method_version: number | string;
  likelihood: number | string;
  impact: number | string;
  score: number | string;
  rationale: string;
  assessor: { display: string };
  assessed_at: string;
};

function toAssessment(item: AssessmentWire): RiskAssessment {
  return {
    assessmentId: item.assessment_id,
    phase: item.phase,
    methodVersion: Number(item.method_version),
    likelihood: Number(item.likelihood),
    impact: Number(item.impact),
    score: Number(item.score),
    rationale: item.rationale,
    assessor: item.assessor.display,
    assessedAt: item.assessed_at,
  };
}

function toTreatment(item: { kind: string; rationale: string; chosen_by: { display: string }; chosen_at: string }): RiskTreatment {
  return { kind: item.kind, rationale: item.rationale, chosenBy: item.chosen_by.display, chosenAt: item.chosen_at };
}

function toAcceptance(item: {
  acceptance_id: string;
  residual_assessment_id: string;
  residual_score: number | string;
  appetite_threshold: number | string | null;
  approver: { display: string };
  approver_authority: string;
  rationale: string;
  accepted_at: string;
  expires_at: string;
  status?: string;
}): RiskAcceptance {
  return {
    acceptanceId: item.acceptance_id,
    residualAssessmentId: item.residual_assessment_id,
    residualScore: Number(item.residual_score),
    appetiteThreshold: toNumber(item.appetite_threshold),
    approver: item.approver.display,
    approverAuthority: item.approver_authority,
    rationale: item.rationale,
    acceptedAt: item.accepted_at,
    expiresAt: item.expires_at,
    status: item.status ?? null,
  };
}

function failure(result: { ok: false; kind: string; status: number; error?: unknown }, action: string) {
  const problem =
    result.kind === 'http' && typeof result.error === 'object' && result.error !== null
      ? (result.error as { detail?: unknown; transient?: unknown })
      : {};
  const transient = problem.transient === true;
  if (result.status === 403) {
    return new RiskRequestError(`You do not have permission to ${action}.`, 403, false);
  }
  if (transient) {
    return new RiskRequestError('The latest changes are still being processed. Try again in a moment.', result.status, true);
  }
  return new RiskRequestError(
    typeof problem.detail === 'string' && problem.detail.length > 0 ? problem.detail : `Unable to ${action}.`,
    result.kind === 'http' ? result.status : null,
    false
  );
}
