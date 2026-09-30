import { createApiClient } from '../../api-client/index.js';
import { requireActiveTenantId } from '../tenants/tenants.js';

const client = createApiClient();

export interface ProgramPlan {
  target_readiness_date: string | null;
  target_type_i_as_of_date: string | null;
  target_type_ii_start_date: string | null;
  target_type_ii_end_date: string | null;
  readiness_advisor: string | null;
  audit_firm: string | null;
}

export const emptyPlan: ProgramPlan = {
  target_readiness_date: null,
  target_type_i_as_of_date: null,
  target_type_ii_start_date: null,
  target_type_ii_end_date: null,
  readiness_advisor: null,
  audit_firm: null,
};

export interface ProgramSummary {
  programId: string;
  name: string;
  stage: string;
  nextStage: string | null;
}

export interface Program extends ProgramSummary {
  revision: number;
  plan: ProgramPlan;
  stagePlan: { stage: string; advanceWhen: string }[];
  lastChangedBy: string;
  lastChangedAt: string;
  criteriaEditionId: string | null;
}

export interface ProgramRevision {
  revision: number;
  name: string;
  plan: ProgramPlan;
  actor: string;
  changedAt: string;
}

export interface SetupWorkItem {
  code: string;
  detail: string;
  sourceType: string;
  sourceId: string | null;
}

// Keeps the HTTP status and the server's transient flag so pages can tell forbidden, conflict,
// and projection lag (a transient 409) apart.
export class ProgramRequestError extends Error {
  constructor(
    message: string,
    readonly status: number | null,
    readonly transient: boolean
  ) {
    super(message);
  }
}

const stageLabels: Record<string, string> = {
  readiness: 'Readiness',
  type_i: 'SOC 2 Type I',
  type_ii: 'SOC 2 Type II',
};

export function stageLabel(stage: string | null | undefined): string {
  if (!stage) return 'None';
  return stageLabels[stage] ?? stage.replaceAll('_', ' ');
}

export async function listPrograms(): Promise<ProgramSummary[]> {
  const tenantId = requireActiveTenantId();
  const programs: ProgramSummary[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listPrograms({ params: { tenant_id: tenantId }, query: { cursor } });
    if (!result.ok) throw programFailure(result, 'load the programs');
    for (const item of result.data?.items ?? []) {
      if (item) {
        programs.push({ programId: item.program_id, name: item.name, stage: item.stage, nextStage: item.next_stage });
      }
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return programs;
}

export async function getProgram(programId: string): Promise<Program> {
  const tenantId = requireActiveTenantId();
  const result = await client.getProgram({ params: { tenant_id: tenantId, program_id: programId } });
  if (!result.ok) throw programFailure(result, 'load this program');
  const data = result.data;
  if (!data) throw new ProgramRequestError('This program was not found.', 404, false);
  return {
    programId: data.program_id,
    name: data.name,
    stage: data.stage,
    nextStage: data.next_stage,
    revision: Number(data.revision),
    plan: data.plan,
    stagePlan: data.stage_plan.filter((s) => s !== null).map((s) => ({ stage: s.stage, advanceWhen: s.advance_when })),
    lastChangedBy: data.last_changed_by?.display ?? data.last_changed_by_display,
    lastChangedAt: data.last_changed_at,
    criteriaEditionId: data.criteria_edition_id ?? null,
  };
}

export async function createProgram(name: string, plan: ProgramPlan): Promise<string> {
  const tenantId = requireActiveTenantId();
  const result = await client.createProgram({ params: { tenant_id: tenantId }, body: { name, plan } });
  if (!result.ok) throw programFailure(result, 'create the program');
  if (!result.data) throw new ProgramRequestError('The program was not created.', null, false);
  return result.data.program_id;
}

export async function reviseProgram(
  programId: string,
  expectedRevision: number,
  name: string,
  plan: ProgramPlan
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.reviseProgram({
    params: { tenant_id: tenantId, program_id: programId },
    body: { expected_revision: expectedRevision, name, plan },
  });
  if (!result.ok) throw programFailure(result, 'save the program');
}

export async function listProgramRevisions(programId: string): Promise<ProgramRevision[]> {
  const tenantId = requireActiveTenantId();
  const revisions: ProgramRevision[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listProgramRevisions({
      params: { tenant_id: tenantId, program_id: programId },
      query: { cursor },
    });
    if (!result.ok) throw programFailure(result, 'load the program history');
    for (const item of result.data?.items ?? []) {
      if (item) {
        revisions.push({
          revision: Number(item.revision),
          name: item.name,
          plan: item.plan,
          actor: item.actor?.display ?? item.actor_display,
          changedAt: item.changed_at,
        });
      }
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return revisions.sort((a, b) => b.revision - a.revision);
}

export async function getSetupWork(programId: string): Promise<SetupWorkItem[]> {
  const tenantId = requireActiveTenantId();
  const result = await client.getProgramSetupWork({ params: { tenant_id: tenantId, program_id: programId } });
  if (!result.ok) throw programFailure(result, 'load the setup work');
  return (result.data?.items ?? [])
    .filter((item) => item !== null)
    .map((item) => ({ code: item.code, detail: item.detail, sourceType: item.source_type, sourceId: item.source_id }));
}

export function programFailure(result: { ok: false; kind: string; status: number; error?: unknown }, action: string) {
  const problem =
    result.kind === 'http' && typeof result.error === 'object' && result.error !== null
      ? (result.error as { detail?: unknown; transient?: unknown })
      : {};
  const transient = problem.transient === true;
  if (result.status === 403) {
    return new ProgramRequestError(`You do not have permission to ${action}.`, 403, false);
  }
  if (transient) {
    return new ProgramRequestError('The latest changes are still being processed. Try again in a moment.', result.status, true);
  }
  return new ProgramRequestError(
    typeof problem.detail === 'string' && problem.detail.length > 0 ? problem.detail : `Unable to ${action}.`,
    result.kind === 'http' ? result.status : null,
    false
  );
}
