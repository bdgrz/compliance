import { createApiClient } from '../../api-client/index.js';
import { requireActiveTenantId } from '../tenants/tenants.js';

const client = createApiClient();

export const componentCategories = ['cloud_account', 'environment', 'network', 'data_store', 'repository', 'endpoint_class'] as const;
export const classifications = ['public', 'internal', 'confidential', 'restricted'] as const;
export const lifecycles = ['active', 'retired'] as const;

export function vocabularyLabel(value: string | null | undefined): string {
  if (!value) return 'None';
  const text = value.replaceAll('_', ' ');
  return text.charAt(0).toUpperCase() + text.slice(1);
}

interface Audit {
  revision: number;
  sourceKind: string;
  lastChangedBy: string;
  lastChangedAt: string;
}

export interface ComponentContent {
  category: string;
  name: string;
  ownerPersonId: string;
  environmentReference: string | null;
  locationReference: string | null;
  systemInstanceId: string | null;
  endpointCount: number | null;
  managementSource: string | null;
  lifecycle: string;
}

export interface TechnologyComponent extends Audit, ComponentContent {
  componentId: string;
}

export interface AssetContent {
  name: string;
  classification: string;
  retentionReference: string;
  ownerPersonId: string;
  description: string | null;
  lifecycle: string;
}

export interface InformationAsset extends Audit, AssetContent {
  informationAssetId: string;
}

export interface FlowContent {
  sourceType: string;
  sourceId: string;
  destinationType: string;
  destinationId: string | null;
  destinationParty: string | null;
  informationAssetIds: string[];
  purpose: string;
  encryptedInTransit: boolean;
  encryptedAtRest: boolean;
  exceptionReference: string | null;
  effectiveFrom: string;
  ownerPersonId: string;
  lifecycle: string;
}

export interface DataFlow extends Audit, FlowContent {
  dataFlowId: string;
  classification: string;
}

export interface AffectedFlow {
  dataFlowId: string;
  recordedClassification: string;
  recomputedClassification: string;
  classificationChanged: boolean;
  encryptionViolation: boolean;
  carriesRetiredAssetOnly: boolean;
}

export interface AssetChangeImpact {
  currentClassification: string;
  proposedClassification: string;
  currentLifecycle: string;
  proposedLifecycle: string;
  affectedFlows: AffectedFlow[];
  flowsOverLimit: boolean;
}

export interface Person {
  personId: string;
  displayName: string;
}

// Keeps the HTTP status and the server's transient flag so pages can tell forbidden, conflict,
// and projection lag (a transient 409) apart.
export class InventoryRequestError extends Error {
  constructor(
    message: string,
    readonly status: number | null,
    readonly transient: boolean
  ) {
    super(message);
  }
}

type Failure = { ok: false; kind: string; status: number; error?: unknown };
type Page<T> = { ok: true; data?: { items: (T | null)[]; next_cursor: string | null } | null } | Failure;
type AuditWire = {
  revision: number | string;
  source_kind: string;
  last_changed_by: { display: string };
  last_changed_at: string;
};

async function collect<T, R>(load: (cursor: string | undefined) => Promise<Page<T>>, map: (item: T) => R, action: string) {
  const items: R[] = [];
  let cursor: string | undefined;
  do {
    const result = await load(cursor);
    if (!result.ok) throw failure(result, action);
    for (const item of result.data?.items ?? []) if (item) items.push(map(item));
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return items;
}

function audit(item: AuditWire): Audit {
  return {
    revision: Number(item.revision),
    sourceKind: item.source_kind,
    lastChangedBy: item.last_changed_by.display,
    lastChangedAt: item.last_changed_at,
  };
}

type ComponentWire = AuditWire & {
  component_id: string;
  content: {
    category: string;
    name: string;
    owner_person_id: string;
    environment_reference: string | null;
    location_reference: string | null;
    system_instance_id: string | null;
    endpoint_count: number | string | null;
    management_source: string | null;
    lifecycle: string;
  };
};

function toComponent(item: ComponentWire): TechnologyComponent {
  const c = item.content;
  return {
    ...audit(item),
    componentId: item.component_id,
    category: c.category,
    name: c.name,
    ownerPersonId: c.owner_person_id,
    environmentReference: c.environment_reference,
    locationReference: c.location_reference,
    systemInstanceId: c.system_instance_id,
    endpointCount: c.endpoint_count === null ? null : Number(c.endpoint_count),
    managementSource: c.management_source,
    lifecycle: c.lifecycle,
  };
}

type AssetWire = AuditWire & {
  information_asset_id: string;
  content: {
    name: string;
    classification: string;
    retention_reference: string;
    owner_person_id: string;
    description: string | null;
    lifecycle: string;
  };
};

function toAsset(item: AssetWire): InformationAsset {
  const c = item.content;
  return {
    ...audit(item),
    informationAssetId: item.information_asset_id,
    name: c.name,
    classification: c.classification,
    retentionReference: c.retention_reference,
    ownerPersonId: c.owner_person_id,
    description: c.description,
    lifecycle: c.lifecycle,
  };
}

type FlowWire = AuditWire & {
  data_flow_id: string;
  content: {
    source_type: string;
    source_id: string;
    destination_type: string;
    destination_id: string | null;
    destination_party: string | null;
    information_asset_ids: string[];
    purpose: string;
    encrypted_in_transit: boolean;
    encrypted_at_rest: boolean;
    exception_reference: string | null;
    effective_from: string;
    owner_person_id: string;
    lifecycle: string;
    classification: string;
  };
};

function toFlow(item: FlowWire): DataFlow {
  const c = item.content;
  return {
    ...audit(item),
    dataFlowId: item.data_flow_id,
    sourceType: c.source_type,
    sourceId: c.source_id,
    destinationType: c.destination_type,
    destinationId: c.destination_id,
    destinationParty: c.destination_party,
    informationAssetIds: c.information_asset_ids,
    purpose: c.purpose,
    encryptedInTransit: c.encrypted_in_transit,
    encryptedAtRest: c.encrypted_at_rest,
    exceptionReference: c.exception_reference,
    effectiveFrom: c.effective_from,
    ownerPersonId: c.owner_person_id,
    lifecycle: c.lifecycle,
    classification: c.classification,
  };
}

function byNewest<T extends { revision: number }>(items: T[]) {
  return items.sort((a, b) => b.revision - a.revision);
}

export function listPeople(): Promise<Person[]> {
  const tenant_id = requireActiveTenantId();
  return collect(
    (cursor) => client.listPeople({ params: { tenant_id }, query: { cursor } }),
    (item) => ({ personId: item.person_id, displayName: item.display_name }),
    'load the people directory'
  );
}

export function listComponents(): Promise<TechnologyComponent[]> {
  const tenant_id = requireActiveTenantId();
  return collect(
    (cursor) => client.listTechnologyComponents({ params: { tenant_id }, query: { cursor } }),
    toComponent,
    'load the technology components'
  );
}

export async function listComponentRevisions(componentId: string): Promise<TechnologyComponent[]> {
  const tenant_id = requireActiveTenantId();
  return byNewest(
    await collect(
      (cursor) => client.listTechnologyComponentRevisions({ params: { tenant_id, component_id: componentId }, query: { cursor } }),
      toComponent,
      'load the component history'
    )
  );
}

function componentBody(content: ComponentContent) {
  return {
    name: content.name,
    owner_person_id: content.ownerPersonId,
    environment_reference: content.environmentReference,
    location_reference: content.locationReference,
    ...(content.category === 'endpoint_class'
      ? { endpoint_count: content.endpointCount ?? 0, management_source: content.managementSource }
      : {}),
  };
}

export async function recordComponent(content: ComponentContent): Promise<string> {
  const tenant_id = requireActiveTenantId();
  const result = await client.recordTechnologyComponent({
    params: { tenant_id },
    body: {
      category: content.category,
      ...componentBody(content),
      ...(content.category === 'cloud_account' && content.systemInstanceId ? { system_instance_id: content.systemInstanceId } : {}),
    },
  });
  if (!result.ok) throw failure(result, 'record the component');
  if (!result.data) throw new InventoryRequestError('The component was not recorded.', null, false);
  return result.data.component_id;
}

export async function reviseComponent(componentId: string, expectedRevision: number, content: ComponentContent) {
  const tenant_id = requireActiveTenantId();
  const result = await client.reviseTechnologyComponent({
    params: { tenant_id, component_id: componentId },
    body: { expected_revision: expectedRevision, lifecycle: content.lifecycle, ...componentBody(content) },
  });
  if (!result.ok) throw failure(result, 'save the component');
}

export function listAssets(): Promise<InformationAsset[]> {
  const tenant_id = requireActiveTenantId();
  return collect(
    (cursor) => client.listInformationAssets({ params: { tenant_id }, query: { cursor } }),
    toAsset,
    'load the information assets'
  );
}

export async function listAssetRevisions(assetId: string): Promise<InformationAsset[]> {
  const tenant_id = requireActiveTenantId();
  return byNewest(
    await collect(
      (cursor) =>
        client.listInformationAssetRevisions({ params: { tenant_id, information_asset_id: assetId }, query: { cursor } }),
      toAsset,
      'load the asset history'
    )
  );
}

function assetBody(content: AssetContent) {
  return {
    name: content.name,
    classification: content.classification,
    retention_reference: content.retentionReference,
    owner_person_id: content.ownerPersonId,
    description: content.description,
  };
}

export async function recordAsset(content: AssetContent): Promise<string> {
  const tenant_id = requireActiveTenantId();
  const result = await client.recordInformationAsset({ params: { tenant_id }, body: assetBody(content) });
  if (!result.ok) throw failure(result, 'record the information asset');
  if (!result.data) throw new InventoryRequestError('The information asset was not recorded.', null, false);
  return result.data.information_asset_id;
}

export async function reviseAsset(assetId: string, expectedRevision: number, content: AssetContent) {
  const tenant_id = requireActiveTenantId();
  const result = await client.reviseInformationAsset({
    params: { tenant_id, information_asset_id: assetId },
    body: { expected_revision: expectedRevision, lifecycle: content.lifecycle, ...assetBody(content) },
  });
  if (!result.ok) throw failure(result, 'save the information asset');
}

export async function previewAssetChange(
  assetId: string,
  expectedRevision: number,
  change: { classification: string; lifecycle: string }
): Promise<AssetChangeImpact> {
  const result = await client.previewInformationAssetChange({
    params: { tenant_id: requireActiveTenantId(), information_asset_id: assetId },
    body: { expected_revision: expectedRevision, classification: change.classification, lifecycle: change.lifecycle },
  });
  if (!result.ok) throw failure(result, 'preview the impact of this change');
  const data = result.data;
  if (!data) throw new InventoryRequestError('This information asset was not found.', 404, false);
  return {
    currentClassification: data.current_classification,
    proposedClassification: data.proposed_classification,
    currentLifecycle: data.current_lifecycle,
    proposedLifecycle: data.proposed_lifecycle,
    affectedFlows: data.affected_flows
      .filter((f) => f !== null)
      .map((f) => ({
        dataFlowId: f.data_flow_id,
        recordedClassification: f.recorded_classification,
        recomputedClassification: f.recomputed_classification,
        classificationChanged: f.classification_changed,
        encryptionViolation: f.encryption_violation,
        carriesRetiredAssetOnly: f.carries_retired_asset_only,
      })),
    flowsOverLimit: data.flows_over_limit,
  };
}

export function listFlows(): Promise<DataFlow[]> {
  const tenant_id = requireActiveTenantId();
  return collect(
    (cursor) => client.listDataFlows({ params: { tenant_id }, query: { cursor } }),
    toFlow,
    'load the data flows'
  );
}

export async function listFlowRevisions(flowId: string): Promise<DataFlow[]> {
  const tenant_id = requireActiveTenantId();
  return byNewest(
    await collect(
      (cursor) => client.listDataFlowRevisions({ params: { tenant_id, data_flow_id: flowId }, query: { cursor } }),
      toFlow,
      'load the data flow history'
    )
  );
}

function flowBody(content: FlowContent) {
  return {
    source_type: content.sourceType,
    source_id: content.sourceId,
    destination_type: content.destinationType,
    ...(content.destinationType === 'external_party'
      ? { destination_party: content.destinationParty }
      : { destination_id: content.destinationId ?? '' }),
    information_asset_ids: content.informationAssetIds,
    purpose: content.purpose,
    encrypted_in_transit: content.encryptedInTransit,
    encrypted_at_rest: content.encryptedAtRest,
    exception_reference: content.exceptionReference,
    effective_from: content.effectiveFrom,
    owner_person_id: content.ownerPersonId,
  };
}

export async function recordFlow(content: FlowContent): Promise<string> {
  const tenant_id = requireActiveTenantId();
  const result = await client.recordDataFlow({ params: { tenant_id }, body: flowBody(content) });
  if (!result.ok) throw failure(result, 'record the data flow');
  if (!result.data) throw new InventoryRequestError('The data flow was not recorded.', null, false);
  return result.data.data_flow_id;
}

export async function reviseFlow(flowId: string, expectedRevision: number, content: FlowContent) {
  const tenant_id = requireActiveTenantId();
  const result = await client.reviseDataFlow({
    params: { tenant_id, data_flow_id: flowId },
    body: { expected_revision: expectedRevision, lifecycle: content.lifecycle, ...flowBody(content) },
  });
  if (!result.ok) throw failure(result, 'save the data flow');
}

function failure(result: Failure, action: string) {
  const problem =
    result.kind === 'http' && typeof result.error === 'object' && result.error !== null
      ? (result.error as { detail?: unknown; transient?: unknown })
      : {};
  const transient = problem.transient === true;
  if (result.status === 403) {
    return new InventoryRequestError(`You do not have permission to ${action}.`, 403, false);
  }
  if (transient) {
    return new InventoryRequestError('The latest changes are still being processed. Try again in a moment.', result.status, true);
  }
  return new InventoryRequestError(
    typeof problem.detail === 'string' && problem.detail.length > 0 ? problem.detail : `Unable to ${action}.`,
    result.kind === 'http' ? result.status : null,
    false
  );
}
