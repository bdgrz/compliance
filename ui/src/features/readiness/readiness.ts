import { createApiClient } from '../../api-client/index.js';
import { listMemberOptions } from '../boundaries/responsibilities.js';
import { getMemberAccess } from '../members/members.js';
import { requireActiveTenantId } from '../tenants/tenants.js';

const client = createApiClient();

export type PlanState = 'all' | 'planned' | 'unplanned';
export type DecisionOutcome = 'proceed' | 'do_not_proceed';

export interface AssessmentSummary {
  assessmentId: string;
  ruleVersion: string;
  asOf: string;
  ruleMetCount: number;
  gapCount: number;
  runBy: string;
  runAt: string;
  decisionOutcome: string | null;
}

export interface SourceReference {
  kind: string;
  id: string;
  version: string | null;
}

export interface ReadinessInput {
  family: string;
  status: string;
  recordCount: number;
  explanation: string;
}

export interface ReadinessFinding {
  criterionIdentifier: string;
  category: string;
  summary: string;
  ruleId: string;
  outcome: string;
  explanation: string;
  sources: SourceReference[];
  gapId: string | null;
}

export interface GapPlan {
  ownerMemberId: string;
  targetDate: string;
  action: string;
  plannedBy: string;
  plannedAt: string;
}

export interface ReadinessGap {
  gapId: string;
  kind: string;
  subject: string;
  ruleId: string;
  explanation: string;
  sources: SourceReference[];
  plan: GapPlan | null;
}

export interface ReadinessDecision {
  outcome: string;
  rationale: string;
  deciderMemberId: string;
  decidedBy: string;
  decidedAt: string;
  waiverId: string | null;
}

export interface ReadinessAssessment {
  assessmentId: string;
  revision: number;
  ruleVersion: string;
  asOf: string;
  inputFingerprint: string;
  inputs: ReadinessInput[];
  findings: ReadinessFinding[];
  gaps: ReadinessGap[];
  ruleMetCount: number;
  gapCount: number;
  runBy: string;
  runAt: string;
  decision: ReadinessDecision | null;
}

// Keeps the HTTP status and the server's transient flag so pages can tell forbidden, not found,
// stale revisions, and projection lag (a transient 409) apart.
export class ReadinessRequestError extends Error {
  constructor(
    message: string,
    readonly status: number | null,
    readonly transient: boolean
  ) {
    super(message);
  }
}

const outcomeLabels: Record<string, string> = {
  rule_met: 'Rule met',
  gap: 'Gap',
};

export function findingOutcomeLabel(outcome: string): string {
  return outcomeLabels[outcome] ?? outcome.replaceAll('_', ' ');
}

const gapKindLabels: Record<string, string> = {
  unmapped_criterion: 'No accepted control mapping',
  no_effective_control: 'No effective control version',
  input_not_assessed: 'Input not assessed',
};

export function gapKindLabel(kind: string): string {
  return gapKindLabels[kind] ?? kind.replaceAll('_', ' ');
}

export const decisionLabels: Record<DecisionOutcome, string> = {
  proceed: 'Proceed',
  do_not_proceed: 'Do not proceed',
};

export function decisionLabel(outcome: string): string {
  return decisionLabels[outcome as DecisionOutcome] ?? outcome.replaceAll('_', ' ');
}

export function familyLabel(family: string): string {
  return family.replaceAll('_', ' ');
}

export async function listAssessments(programId: string): Promise<AssessmentSummary[]> {
  const tenantId = requireActiveTenantId();
  const items: AssessmentSummary[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listReadinessAssessments({
      params: { tenant_id: tenantId, program_id: programId },
      query: { cursor },
    });
    if (!result.ok) throw failure(result, 'load the readiness assessments');
    for (const item of result.data?.items ?? []) {
      if (!item) continue;
      items.push({
        assessmentId: item.assessment_id,
        ruleVersion: item.rule_version,
        asOf: item.as_of,
        ruleMetCount: Number(item.rule_met_count),
        gapCount: Number(item.gap_count),
        runBy: item.run_by.display,
        runAt: item.run_at,
        decisionOutcome: item.decision_outcome,
      });
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return items;
}

export async function getAssessment(programId: string, assessmentId: string): Promise<ReadinessAssessment> {
  const tenantId = requireActiveTenantId();
  const result = await client.getReadinessAssessment({
    params: { tenant_id: tenantId, program_id: programId, assessment_id: assessmentId },
  });
  if (!result.ok) throw failure(result, 'load the readiness assessment');
  const data = result.data;
  if (!data) throw new ReadinessRequestError('This assessment was not found.', 404, false);
  return {
    assessmentId: data.assessment_id,
    revision: Number(data.revision),
    ruleVersion: data.rule_version,
    asOf: data.as_of,
    inputFingerprint: data.input_fingerprint,
    inputs: data.inputs
      .filter((item) => item !== null)
      .map((item) => ({
        family: item.family,
        status: item.status,
        recordCount: Number(item.record_count),
        explanation: item.explanation,
      })),
    findings: data.findings
      .filter((item) => item !== null)
      .map((item) => ({
        criterionIdentifier: item.criterion_identifier,
        category: item.category,
        summary: item.summary,
        ruleId: item.rule_id,
        outcome: item.outcome,
        explanation: item.explanation,
        sources: toSources(item.sources),
        gapId: item.gap_id,
      })),
    gaps: data.gaps.filter((item) => item !== null).map(toGap),
    ruleMetCount: Number(data.rule_met_count),
    gapCount: Number(data.gap_count),
    runBy: data.run_by.display,
    runAt: data.run_at,
    decision: data.decision
      ? {
          outcome: data.decision.outcome,
          rationale: data.decision.rationale,
          deciderMemberId: data.decision.decider_member_id,
          decidedBy: data.decision.decided_by.display,
          decidedAt: data.decision.decided_at,
          waiverId: data.decision.separation_of_duties_waiver_id,
        }
      : null,
  };
}

export async function listGaps(programId: string, assessmentId: string, planState: PlanState): Promise<ReadinessGap[]> {
  const tenantId = requireActiveTenantId();
  const gaps: ReadinessGap[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listReadinessGaps({
      params: { tenant_id: tenantId, program_id: programId, assessment_id: assessmentId },
      query: planState === 'all' ? { cursor } : { cursor, plan_state: planState },
    });
    if (!result.ok) throw failure(result, 'load the gap plan');
    for (const item of result.data?.items ?? []) {
      if (item) gaps.push(toGap(item));
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return gaps;
}

export async function runAssessment(programId: string, expectedRevision: number, asOf: string | null): Promise<string> {
  const tenantId = requireActiveTenantId();
  const result = await client.runReadinessAssessment({
    params: { tenant_id: tenantId, program_id: programId },
    body: asOf === null ? { expected_revision: expectedRevision } : { expected_revision: expectedRevision, as_of: asOf },
  });
  if (!result.ok) throw failure(result, 'run the readiness assessment');
  if (!result.data) throw new ReadinessRequestError('The assessment was not recorded.', null, false);
  return result.data.assessment_id;
}

export async function planGap(
  programId: string,
  gapId: string,
  expectedRevision: number,
  plan: { ownerMemberId: string; targetDate: string; action: string }
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.planReadinessGap({
    params: { tenant_id: tenantId, program_id: programId, gap_id: gapId },
    body: {
      expected_revision: expectedRevision,
      owner_member_id: plan.ownerMemberId,
      target_date: plan.targetDate,
      action: plan.action,
    },
  });
  if (!result.ok) throw failure(result, 'plan the gap');
}

export async function decideReadiness(
  programId: string,
  assessmentId: string,
  expectedRevision: number,
  decision: { outcome: DecisionOutcome; rationale: string; waiverId: string | null }
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.decideReadiness({
    params: { tenant_id: tenantId, program_id: programId, assessment_id: assessmentId },
    body: {
      expected_revision: expectedRevision,
      outcome: decision.outcome,
      rationale: decision.rationale,
      ...(decision.waiverId ? { separation_of_duties_waiver_id: decision.waiverId } : {}),
    },
  });
  if (!result.ok) {
    if (result.status === 403) {
      throw new ReadinessRequestError(
        'You cannot record this decision. The member who ran an assessment cannot decide it without an approved separation-of-duties waiver, and deciding needs the program manage permission.',
        403,
        false
      );
    }
    throw failure(result, 'record the readiness decision');
  }
}

export interface OwnerOption {
  memberId: string;
  label: string;
}

// Gap owners are tenant members; the plan names a member id, which the member access view
// resolves for each selectable person. People whose access cannot be read are left out.
export async function listOwnerOptions(): Promise<OwnerOption[]> {
  const people = await listMemberOptions();
  const resolved = await Promise.all(
    people.map(async (person) => {
      try {
        const access = await getMemberAccess(person.userId);
        return { memberId: access.member_id, label: person.label };
      } catch {
        return null;
      }
    })
  );
  return resolved.filter((option) => option !== null).sort((a, b) => a.label.localeCompare(b.label));
}

type SourceWire =Array<{ kind: string; id: string; version: string | null } | null>;

function toSources(sources: SourceWire): SourceReference[] {
  return sources.filter((s) => s !== null).map((s) => ({ kind: s.kind, id: s.id, version: s.version }));
}

function toGap(item: {
  gap_id: string;
  kind: string;
  subject: string;
  rule_id: string;
  explanation: string;
  sources: SourceWire;
  plan?: {
    owner_member_id: string;
    target_date: string;
    action: string;
    planned_by: { display: string };
    planned_at: string;
  } | null;
}): ReadinessGap {
  return {
    gapId: item.gap_id,
    kind: item.kind,
    subject: item.subject,
    ruleId: item.rule_id,
    explanation: item.explanation,
    sources: toSources(item.sources),
    plan: item.plan
      ? {
          ownerMemberId: item.plan.owner_member_id,
          targetDate: item.plan.target_date,
          action: item.plan.action,
          plannedBy: item.plan.planned_by.display,
          plannedAt: item.plan.planned_at,
        }
      : null,
  };
}

function failure(result: { ok: false; kind: string; status: number; error?: unknown }, action: string) {
  const problem =
    result.kind === 'http' && typeof result.error === 'object' && result.error !== null
      ? (result.error as { detail?: unknown; transient?: unknown })
      : {};
  if (result.status === 403) {
    return new ReadinessRequestError(`You do not have permission to ${action}.`, 403, false);
  }
  if (problem.transient === true) {
    return new ReadinessRequestError('The latest changes are still being processed. Try again in a moment.', result.status, true);
  }
  return new ReadinessRequestError(
    typeof problem.detail === 'string' && problem.detail.length > 0 ? problem.detail : `Unable to ${action}.`,
    result.kind === 'http' ? result.status : null,
    false
  );
}
