import { createApiClient } from '../../api-client/index.js';
import { programFailure, ProgramRequestError } from '../programs/programs.js';
import { requireActiveTenantId } from '../tenants/tenants.js';

const client = createApiClient();

export interface ApplicabilityReference {
  entry_id: string;
  subject_type: string;
  subject: string;
  governed_record_id: string | null;
  rationale: string;
  unresolved: boolean;
}

export interface ControlContent {
  title: string;
  objective: string;
  description: string;
  implementationNarrative: string;
  expectedEvidence: string[];
  ownerReference: string | null;
  applicability: ApplicabilityReference[];
}

export interface ControlDraft {
  controlId: string;
  identifier: string;
  revision: number;
  status: string;
  ownerResolution: string;
  applicabilityResolution: string;
  content: ControlContent;
  lastChangedBy: string;
  lastChangedAt: string;
}

export interface ControlDraftRevision {
  revision: number;
  content: ControlContent;
  actor: string;
  changedAt: string;
}

export const emptyContent: ControlContent = {
  title: '',
  objective: '',
  description: '',
  implementationNarrative: '',
  expectedEvidence: [],
  ownerReference: null,
  applicability: [],
};

type ContentData = {
  title: string;
  objective: string;
  description: string;
  implementation_narrative: string;
  expected_evidence_descriptions: (string | null)[];
  owner_reference?: string | null;
  applicability?: (ApplicabilityReference | null)[] | null;
};

type DraftData = {
  control_id: string;
  identifier: string;
  revision: number | string;
  status: string;
  owner_resolution: string;
  applicability_resolution: string;
  content: ContentData;
  last_changed_by_display: string;
  last_changed_at: string;
  last_changed_by?: { display: string } | null;
};

function toContent(data: ContentData): ControlContent {
  return {
    title: data.title,
    objective: data.objective,
    description: data.description,
    implementationNarrative: data.implementation_narrative,
    expectedEvidence: data.expected_evidence_descriptions.filter((value) => value !== null),
    ownerReference: data.owner_reference ?? null,
    applicability: (data.applicability ?? []).filter((value) => value !== null),
  };
}

function toBody(content: ControlContent) {
  return {
    title: content.title.trim(),
    objective: content.objective.trim(),
    description: content.description.trim(),
    implementation_narrative: content.implementationNarrative.trim(),
    expected_evidence_descriptions: content.expectedEvidence.map((value) => value.trim()).filter((value) => value !== ''),
    owner_reference: content.ownerReference?.trim() ? content.ownerReference.trim() : null,
    applicability: content.applicability,
  };
}

function toDraft(data: DraftData): ControlDraft {
  return {
    controlId: data.control_id,
    identifier: data.identifier,
    revision: Number(data.revision),
    status: data.status,
    ownerResolution: data.owner_resolution,
    applicabilityResolution: data.applicability_resolution,
    content: toContent(data.content),
    lastChangedBy: data.last_changed_by?.display ?? data.last_changed_by_display,
    lastChangedAt: data.last_changed_at,
  };
}

export async function listControlDrafts(programId: string): Promise<ControlDraft[]> {
  const tenantId = requireActiveTenantId();
  const drafts: ControlDraft[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listControlDrafts({
      params: { tenant_id: tenantId, program_id: programId },
      query: { cursor },
    });
    if (!result.ok) throw programFailure(result, 'load the controls');
    for (const item of result.data?.items ?? []) {
      if (item) drafts.push(toDraft(item));
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return drafts;
}

// `minimumRevision` asks the server to wait for (or report lag behind) a revision this client just wrote.
export async function getControlDraft(programId: string, controlId: string, minimumRevision?: number): Promise<ControlDraft> {
  const tenantId = requireActiveTenantId();
  const result = await client.getControlDraft({
    params: { tenant_id: tenantId, program_id: programId, control_id: controlId },
    query: { minimum_revision: minimumRevision },
  });
  if (!result.ok) throw programFailure(result, 'load this control');
  if (!result.data) throw new ProgramRequestError('This control was not found.', 404, false);
  return toDraft(result.data);
}

export async function createControlDraft(programId: string, identifier: string, content: ControlContent): Promise<string> {
  const tenantId = requireActiveTenantId();
  const result = await client.createControlDraft({
    params: { tenant_id: tenantId, program_id: programId },
    body: { identifier: identifier.trim(), content: toBody(content) },
  });
  if (!result.ok) throw programFailure(result, 'create the control');
  if (!result.data) throw new ProgramRequestError('The control was not created.', null, false);
  return result.data.control_id;
}

export async function reviseControlDraft(
  programId: string,
  controlId: string,
  expectedRevision: number,
  content: ControlContent
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.reviseControlDraft({
    params: { tenant_id: tenantId, program_id: programId, control_id: controlId },
    body: { expected_revision: expectedRevision, content: toBody(content) },
  });
  if (!result.ok) throw programFailure(result, 'save the control');
}

export async function listControlDraftRevisions(programId: string, controlId: string): Promise<ControlDraftRevision[]> {
  const tenantId = requireActiveTenantId();
  const revisions: ControlDraftRevision[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listControlDraftRevisions({
      params: { tenant_id: tenantId, program_id: programId, control_id: controlId },
      query: { cursor },
    });
    if (!result.ok) throw programFailure(result, 'load the control history');
    for (const item of result.data?.items ?? []) {
      if (item) {
        revisions.push({
          revision: Number(item.revision),
          content: toContent(item.content),
          actor: item.actor?.display ?? item.changed_by_display,
          changedAt: item.changed_at,
        });
      }
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return revisions.sort((a, b) => b.revision - a.revision);
}

export { ProgramRequestError };
