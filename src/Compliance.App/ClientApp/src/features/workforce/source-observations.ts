import {
  createClient,
  defineApi,
  empty,
  get,
  json,
  post,
  put,
} from '@askrjs/fetch';

import { requireActiveTenantId } from '../tenants/tenants.js';
import { failure } from './workforce.js';

export type SourceTargetKind =
  | 'person'
  | 'work_relationship'
  | 'service_identity';
export interface SourceTarget {
  kind: SourceTargetKind;
  id: string;
  revision: number;
  facts: SourceFacts;
}
export interface SourceIdentity {
  source_kind: 'hris' | 'idp' | 'provider';
  source_system: string;
  source_record_id: string;
  source_revision: string;
}
export interface SourceDecision {
  outcome: 'accepted' | 'dismissed';
  note: string;
  target_revision: number;
  actor: { display: string };
  decided_at: string;
}
export interface SourceFacts {
  person?: { display_name: string; work_email: string | null } | null;
  work_relationship?: {
    worker_type: string;
    lifecycle_status: string;
    start_date: string;
    end_date: string | null;
    department: string | null;
    manager_person_id: string | null;
    sponsor_person_id: string | null;
    employment_status_reason?: string | null;
  } | null;
  service_identity?: {
    display_name: string;
    identity_kind: string;
    environment: string | null;
    lifecycle_status: string;
    expires_on: string | null;
  } | null;
}
export interface SourceObservation {
  observation_id: string;
  revision: number;
  source: SourceIdentity;
  target_kind: SourceTargetKind;
  target_id: string;
  observed_target_revision: number;
  facts: SourceFacts;
  observed_at: string;
  recorded_by: { display: string };
  recorded_at: string;
  decision?: SourceDecision | null;
  restricted_fields_redacted?: boolean;
  current_target_revision?: number | null;
  accepted_for_current_revision?: boolean | null;
}
export interface SourcePreview {
  observation_id: string;
  revision: number;
  source: SourceIdentity;
  target_kind: SourceTargetKind;
  target_id: string;
  observed_target_revision: number;
  current_target_revision: number;
  source_authority: string;
  conflicting_fields: string[];
  restricted_fields_conflict: boolean;
  can_accept: boolean;
  accepted_for_current_revision: boolean;
  decision: SourceDecision | null;
}

type Problem = { detail?: string; transient?: boolean };
const errors = {
  400: json<Problem>(),
  403: json<Problem>(),
  404: json<Problem>(),
  409: json<Problem>(),
};
const collection = '/api/v1/tenants/{tenant_id}/workforce-source-observations';

// Feature-local contracts mirror WorkforceSource* in Compliance.Common. The generated client
// predates these endpoints; use the same typed HTTP transport until its next full regeneration.
const client = createClient(
  defineApi({
    list: get(collection)
      .params<{ tenant_id: string }>()
      .query<{
        target_kind?: SourceTargetKind;
        target_id?: string;
        cursor?: string;
        limit: number;
      }>()
      .returns(
        json<{ items: SourceObservation[]; next_cursor?: string | null }>()
      )
      .errors(errors),
    get: get(`${collection}/{observation_id}`)
      .params<{ tenant_id: string; observation_id: string }>()
      .returns(json<SourceObservation>())
      .errors(errors),
    preview: get(`${collection}/{observation_id}/preview`)
      .params<{ tenant_id: string; observation_id: string }>()
      .returns(json<SourcePreview>())
      .errors(errors),
    decide: put(`${collection}/{observation_id}/decision`)
      .params<{ tenant_id: string; observation_id: string }>()
      .body(
        json<{
          expected_revision: number;
          expected_target_revision: number;
          outcome: 'accepted' | 'dismissed';
          note: string;
        }>()
      )
      .returns(204, empty())
      .errors(errors),
    record: post(collection)
      .params<{ tenant_id: string }>()
      .body(
        json<{
          source: SourceIdentity;
          target_kind: SourceTargetKind;
          target_id: string;
          expected_target_revision: number;
          facts: SourceFacts;
          observed_at: string;
        }>()
      )
      .returns(json<{ observation_id: string }>())
      .errors(errors),
  })
);

export async function recordSourceObservation(
  target: SourceTarget,
  source: SourceIdentity,
  facts: SourceFacts,
  observedAt: string
): Promise<string> {
  const result = await client.record({
    params: { tenant_id: requireActiveTenantId() },
    body: {
      source,
      target_kind: target.kind,
      target_id: target.id,
      expected_target_revision: target.revision,
      facts,
      observed_at: observedAt,
    },
  });
  if (!result.ok) throw failure(result, 'record this source observation');
  return result.data.observation_id;
}

export async function decideSourceObservation(
  preview: SourcePreview,
  outcome: 'accepted' | 'dismissed',
  note: string
): Promise<void> {
  const result = await client.decide({
    params: {
      tenant_id: requireActiveTenantId(),
      observation_id: preview.observation_id,
    },
    body: {
      expected_revision: preview.revision,
      expected_target_revision: preview.current_target_revision,
      outcome,
      note: note.trim(),
    },
  });
  if (!result.ok) throw failure(result, 'decide this source observation');
}

export async function getSourceObservation(
  observationId: string
): Promise<SourceObservation> {
  const result = await client.get({
    params: {
      tenant_id: requireActiveTenantId(),
      observation_id: observationId,
    },
  });
  if (!result.ok) throw failure(result, 'read this source observation');
  return result.data;
}

export async function previewSourceObservation(
  observationId: string
): Promise<SourcePreview> {
  const result = await client.preview({
    params: {
      tenant_id: requireActiveTenantId(),
      observation_id: observationId,
    },
  });
  if (!result.ok) throw failure(result, 'compare this source observation');
  return result.data;
}

export async function listSourceObservations(target?: {
  kind: SourceTargetKind;
  id: string;
}): Promise<SourceObservation[]> {
  const items: SourceObservation[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.list({
      params: { tenant_id: requireActiveTenantId() },
      query: {
        limit: 200,
        ...(target ? { target_kind: target.kind, target_id: target.id } : {}),
        ...(cursor ? { cursor } : {}),
      },
    });
    if (!result.ok) throw failure(result, 'read source observations');
    items.push(...result.data.items);
    cursor = result.data.next_cursor ?? undefined;
  } while (cursor);
  return items;
}

export function sourceLabel(kind: string): string {
  return (
    (
      {
        hris: 'HRIS',
        idp: 'Identity provider',
        provider: 'Provider',
      } as Record<string, string>
    )[kind] ?? kind
  );
}

export function sourceTargetPath(kind: SourceTargetKind, id: string): string {
  const section = {
    person: 'people',
    work_relationship: 'relationships',
    service_identity: 'service-identities',
  }[kind];
  return `/workforce/${section}/${id}`;
}
