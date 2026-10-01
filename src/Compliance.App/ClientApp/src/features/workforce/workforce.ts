import { createApiClient } from '../../api-client/index.js';
import { requireActiveTenantId } from '../tenants/tenants.js';

const client = createApiClient();

export interface Person {
  personId: string;
  revision: number;
  displayName: string;
  workEmail: string | null;
  sourceKind: string;
  lastChangedBy: string;
  lastChangedAt: string;
  correlatedUserId: string | null;
}

export interface WorkRelationshipTerms {
  workerType: string;
  lifecycleStatus: string;
  startDate: string;
  endDate: string | null;
  department: string | null;
  managerPersonId: string | null;
  sponsorPersonId: string | null;
}

export interface WorkRelationship extends WorkRelationshipTerms {
  relationshipId: string;
  revision: number;
  personId: string;
  sourceWorkerId: string;
  restrictedFieldsRedacted: boolean;
  sourceKind: string;
  lastChangedBy: string;
  lastChangedAt: string;
}

export interface WorkforceObservation {
  observationId: string;
  kind: string;
  status: string;
  relationshipId: string;
  personId: string;
  sourceWorkerId: string;
  effectiveDate: string;
  changedFields: string[];
  observedFrom: string;
  observedAt: string;
  resolution: ObservationResolution | null;
}

export interface ObservationResolution {
  resolution: string;
  note: string;
  resolvedBy: string;
  resolvedAt: string;
}

export interface ReconciliationObservation {
  observationId: string;
  kind: string;
  reason: string;
  status: string;
  personIds: string[];
  relationshipIds: string[];
  userIds: string[];
  resolution: ObservationResolution | null;
}

export interface RosterSnapshotSummary {
  snapshotId: string;
  rootSnapshotId: string;
  amendsSnapshotId: string | null;
  kind: string;
  rowCount: number;
  contentSha256: string;
  amendmentReason: string | null;
  frozenBy: string;
  frozenAt: string;
}

export interface RosterSnapshotPerson {
  personId: string;
  revision: number;
  displayName: string;
  workEmail: string | null;
}

export interface RosterSnapshotRelationship {
  relationshipId: string;
  revision: number;
  personId: string;
  sourceWorkerId: string;
  workerType: string;
  lifecycleStatus: string;
  startDate: string;
  endDate: string | null;
  department: string | null;
  managerPersonId: string | null;
  sponsorPersonId: string | null;
}

export interface RosterSnapshot extends Omit<RosterSnapshotSummary, 'kind'> {
  people: RosterSnapshotPerson[];
  relationships: RosterSnapshotRelationship[];
  restrictedFieldsRedacted: boolean;
}

export interface ServiceIdentityTerms {
  displayName: string;
  identityKind: string;
  purpose: string;
  environment: string | null;
  ownerKind: string;
  ownerId: string;
  reviewBy: string;
  expiresOn: string | null;
}

export interface ServiceIdentity extends ServiceIdentityTerms {
  serviceIdentityId: string;
  revision: number;
  lifecycleStatus: string;
  sourceKind: string;
  unowned: boolean;
  unownedReasons: string[];
  expired: boolean;
  lastChangedBy: string;
  lastChangedAt: string;
}

// Keeps the HTTP status and the server's transient flag so pages can tell forbidden, not found,
// stale-revision conflicts, and projection lag (a transient 409) apart.
export class WorkforceRequestError extends Error {
  constructor(
    message: string,
    readonly status: number | null,
    readonly transient: boolean
  ) {
    super(message);
  }
}

export const workerTypes = [
  { value: 'employee', label: 'Employee' },
  { value: 'contractor', label: 'Contractor' },
  { value: 'external_collaborator', label: 'External collaborator' },
];

export const lifecycleStatuses = [
  { value: 'pending', label: 'Pending start' },
  { value: 'active', label: 'Active' },
  { value: 'on_leave', label: 'On leave' },
  { value: 'ended', label: 'Ended (leaver)' },
];

export const observationKinds = [
  { value: 'joiner', label: 'Joiner' },
  { value: 'mover', label: 'Mover' },
  { value: 'leaver', label: 'Leaver' },
];

export const identityKinds = [
  { value: 'workload', label: 'Workload' },
  { value: 'service', label: 'Service account' },
  { value: 'automation', label: 'Automation' },
  { value: 'bot', label: 'Bot' },
  { value: 'integration', label: 'Integration' },
];

export const identityLifecycles = [
  { value: 'active', label: 'Active' },
  { value: 'disabled', label: 'Disabled' },
  { value: 'retired', label: 'Retired' },
];

const unownedReasonLabels: Record<string, string> = {
  review_expired: 'Ownership review date has passed',
  owner_relationship_ended: "The owner's work relationship has ended",
  owner_not_on_roster: 'The owner is not on the workforce roster',
  owner_team_deleted: 'The owning team was deleted',
};

const sourceKindLabels: Record<string, string> = {
  manual: 'Manual roster (authoritative source)',
};

// M0-D06 source precedence, highest first. Only the manual roster is recorded today; an HRIS
// export would outrank it once an import exists (#347), and the identity provider only corroborates.
export const sourcePrecedence = [
  {
    kind: 'hris',
    label: 'HRIS roster export',
    role: 'Authoritative when the organization has an HRIS. No HRIS import is connected yet.',
  },
  {
    kind: 'manual',
    label: 'Manual roster',
    role: 'Authoritative when no HRIS exists. Every roster record today comes from this source.',
  },
  {
    kind: 'identity_provider',
    label: 'Identity-provider directory',
    role: 'Corroborates the roster only; it never overrides an authoritative source.',
  },
];

export const reconciliationKinds = [
  { value: 'missing', label: 'Missing' },
  { value: 'duplicate', label: 'Duplicate' },
  { value: 'conflicting', label: 'Conflicting' },
  { value: 'stale', label: 'Stale' },
  { value: 'access_only', label: 'Access only' },
];

export const observationStatuses = [
  { value: 'open', label: 'Open' },
  { value: 'resolved', label: 'Resolved' },
  { value: 'dismissed', label: 'Dismissed' },
];

const reconciliationReasonLabels: Record<string, string> = {
  no_work_relationship: 'On the roster with no work relationship',
  correlated_member_missing: 'The correlated member is no longer in the organization',
  ended_worker_retains_access: 'Every work relationship has ended but the member still has access',
  member_correlated_to_several_people: 'One member is correlated to several people',
  shared_work_email: 'Several people share a work email',
  end_date_passed: 'The end date has passed but the relationship is not ended',
  start_date_passed: 'The start date has passed but the relationship is still pending',
  member_not_on_roster: 'The member has access but is not on the roster',
};

export function reconciliationReasonLabel(reason: string): string {
  return reconciliationReasonLabels[reason] ?? reason.replaceAll('_', ' ');
}

export function optionLabel(options: { value: string; label: string }[], value: string | null | undefined): string {
  if (!value) return 'Not set';
  return options.find((option) => option.value === value)?.label ?? value.replaceAll('_', ' ');
}

export function unownedReasonLabel(reason: string): string {
  return unownedReasonLabels[reason] ?? reason.replaceAll('_', ' ');
}

export function sourceKindLabel(kind: string): string {
  return sourceKindLabels[kind] ?? kind.replaceAll('_', ' ');
}

export function formatDate(value: string | null): string {
  if (!value) return 'Not set';
  return new Date(`${value.slice(0, 10)}T00:00:00Z`).toLocaleDateString(undefined, { timeZone: 'UTC' });
}

// The latest review date the server accepts: at most one year out.
export function latestReviewDate(today = new Date()): string {
  const limit = new Date(Date.UTC(today.getUTCFullYear() + 1, today.getUTCMonth(), today.getUTCDate()));
  return limit.toISOString().slice(0, 10);
}

type Actor = { display: string };

function toPerson(data: {
  person_id: string;
  revision: number | string;
  display_name: string;
  work_email: string | null;
  source_kind: string;
  last_changed_by: Actor;
  last_changed_at: string;
  correlated_user_id?: string | null;
}): Person {
  return {
    personId: data.person_id,
    revision: Number(data.revision),
    displayName: data.display_name,
    workEmail: data.work_email,
    sourceKind: data.source_kind,
    lastChangedBy: data.last_changed_by.display,
    lastChangedAt: data.last_changed_at,
    correlatedUserId: data.correlated_user_id ?? null,
  };
}

function toRelationship(data: {
  relationship_id: string;
  revision: number | string;
  person_id: string;
  source_worker_id: string;
  worker_type: string;
  lifecycle_status: string;
  start_date: string;
  end_date: string | null;
  department: string | null;
  manager_person_id: string | null;
  sponsor_person_id: string | null;
  restricted_fields_redacted: boolean;
  source_kind: string;
  last_changed_by: Actor;
  last_changed_at: string;
}): WorkRelationship {
  return {
    relationshipId: data.relationship_id,
    revision: Number(data.revision),
    personId: data.person_id,
    sourceWorkerId: data.source_worker_id,
    workerType: data.worker_type,
    lifecycleStatus: data.lifecycle_status,
    startDate: data.start_date,
    endDate: data.end_date,
    department: data.department,
    managerPersonId: data.manager_person_id,
    sponsorPersonId: data.sponsor_person_id,
    restrictedFieldsRedacted: data.restricted_fields_redacted,
    sourceKind: data.source_kind,
    lastChangedBy: data.last_changed_by.display,
    lastChangedAt: data.last_changed_at,
  };
}

function toServiceIdentity(data: {
  service_identity_id: string;
  revision: number | string;
  display_name: string;
  identity_kind: string;
  purpose: string;
  environment: string | null;
  lifecycle_status: string;
  owner_kind: string;
  owner_id: string;
  review_by: string;
  source_kind: string;
  unowned: boolean;
  unowned_reasons: (string | null)[];
  last_changed_by: Actor;
  last_changed_at: string;
  expires_on?: string | null;
  expired?: boolean;
}): ServiceIdentity {
  return {
    serviceIdentityId: data.service_identity_id,
    revision: Number(data.revision),
    displayName: data.display_name,
    identityKind: data.identity_kind,
    purpose: data.purpose,
    environment: data.environment,
    lifecycleStatus: data.lifecycle_status,
    ownerKind: data.owner_kind,
    ownerId: data.owner_id,
    reviewBy: data.review_by,
    sourceKind: data.source_kind,
    unowned: data.unowned,
    unownedReasons: data.unowned_reasons.filter((reason): reason is string => reason !== null),
    expiresOn: data.expires_on ?? null,
    expired: data.expired ?? false,
    lastChangedBy: data.last_changed_by.display,
    lastChangedAt: data.last_changed_at,
  };
}

function optional(value: string | null | undefined): string | undefined {
  return value === null || value === undefined || value.trim() === '' ? undefined : value.trim();
}

export async function listPeople(): Promise<Person[]> {
  const tenantId = requireActiveTenantId();
  const people: Person[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listPeople({ params: { tenant_id: tenantId }, query: { cursor } });
    if (!result.ok) throw failure(result, 'load the workforce roster');
    for (const item of result.data?.items ?? []) {
      if (item) people.push(toPerson(item));
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return people;
}

export async function getPerson(personId: string): Promise<Person> {
  const tenantId = requireActiveTenantId();
  const result = await client.getPerson({ params: { tenant_id: tenantId, person_id: personId } });
  if (!result.ok) throw failure(result, 'load this person');
  if (!result.data) throw new WorkforceRequestError('This person was not found.', 404, false);
  return toPerson(result.data);
}

export async function recordPerson(displayName: string, workEmail: string | null): Promise<string> {
  const tenantId = requireActiveTenantId();
  const result = await client.recordPerson({
    params: { tenant_id: tenantId },
    body: { display_name: displayName.trim(), work_email: optional(workEmail) ?? null },
  });
  if (!result.ok) throw failure(result, 'record the person');
  if (!result.data) throw new WorkforceRequestError('The person was not recorded.', null, false);
  return result.data.person_id;
}

export async function revisePerson(
  personId: string,
  expectedRevision: number,
  displayName: string,
  workEmail: string | null
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.revisePerson({
    params: { tenant_id: tenantId, person_id: personId },
    body: { expected_revision: expectedRevision, display_name: displayName.trim(), work_email: optional(workEmail) ?? null },
  });
  if (!result.ok) throw failure(result, 'save this person');
}

export async function listWorkRelationships(): Promise<WorkRelationship[]> {
  const tenantId = requireActiveTenantId();
  const relationships: WorkRelationship[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listWorkRelationships({ params: { tenant_id: tenantId }, query: { cursor } });
    if (!result.ok) throw failure(result, 'load the work relationships');
    for (const item of result.data?.items ?? []) {
      if (item) relationships.push(toRelationship(item));
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return relationships;
}

export async function getWorkRelationship(relationshipId: string): Promise<WorkRelationship> {
  const tenantId = requireActiveTenantId();
  const result = await client.getWorkRelationship({
    params: { tenant_id: tenantId, relationship_id: relationshipId },
  });
  if (!result.ok) throw failure(result, 'load this work relationship');
  if (!result.data) throw new WorkforceRequestError('This work relationship was not found.', 404, false);
  return toRelationship(result.data);
}

function termsBody(terms: WorkRelationshipTerms) {
  return {
    worker_type: terms.workerType,
    lifecycle_status: terms.lifecycleStatus,
    start_date: terms.startDate,
    end_date: optional(terms.endDate),
    department: optional(terms.department) ?? null,
    manager_person_id: optional(terms.managerPersonId),
    sponsor_person_id: optional(terms.sponsorPersonId),
  };
}

export async function recordWorkRelationship(
  personId: string,
  sourceWorkerId: string,
  terms: WorkRelationshipTerms
): Promise<string> {
  const tenantId = requireActiveTenantId();
  const result = await client.recordWorkRelationship({
    params: { tenant_id: tenantId },
    body: { person_id: personId, source_worker_id: sourceWorkerId.trim(), ...termsBody(terms) },
  });
  if (!result.ok) throw failure(result, 'record the work relationship');
  if (!result.data) throw new WorkforceRequestError('The work relationship was not recorded.', null, false);
  return result.data.relationship_id;
}

export async function reviseWorkRelationship(
  relationshipId: string,
  expectedRevision: number,
  terms: WorkRelationshipTerms
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.reviseWorkRelationship({
    params: { tenant_id: tenantId, relationship_id: relationshipId },
    body: { expected_revision: expectedRevision, ...termsBody(terms) },
  });
  if (!result.ok) throw failure(result, 'save this work relationship');
}

export async function listWorkforceObservations(kind: string | null): Promise<WorkforceObservation[]> {
  const tenantId = requireActiveTenantId();
  const observations: WorkforceObservation[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listWorkforceObservations({
      params: { tenant_id: tenantId },
      query: { cursor, kind: kind ?? undefined },
    });
    if (!result.ok) throw failure(result, 'load the workforce changes');
    for (const item of result.data?.items ?? []) {
      if (item) {
        observations.push({
          observationId: item.observation_id,
          kind: item.kind,
          status: item.status,
          relationshipId: item.relationship_id,
          personId: item.person_id,
          sourceWorkerId: item.source_worker_id,
          effectiveDate: item.effective_date,
          changedFields: item.changed_fields.filter((field): field is string => field !== null),
          observedFrom: item.observed_from.display,
          observedAt: item.observed_at,
          resolution: resolutionOf(item.resolution),
        });
      }
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return observations;
}

export async function listServiceIdentities(unownedOnly: boolean): Promise<ServiceIdentity[]> {
  const tenantId = requireActiveTenantId();
  const identities: ServiceIdentity[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listServiceIdentities({
      params: { tenant_id: tenantId },
      query: { cursor, unowned_only: unownedOnly ? true : undefined },
    });
    if (!result.ok) throw failure(result, 'load the service identities');
    for (const item of result.data?.items ?? []) {
      if (item) identities.push(toServiceIdentity(item));
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return identities;
}

export async function getServiceIdentity(serviceIdentityId: string): Promise<ServiceIdentity> {
  const tenantId = requireActiveTenantId();
  const result = await client.getServiceIdentity({
    params: { tenant_id: tenantId, service_identity_id: serviceIdentityId },
  });
  if (!result.ok) throw failure(result, 'load this service identity');
  if (!result.data) throw new WorkforceRequestError('This service identity was not found.', 404, false);
  return toServiceIdentity(result.data);
}

function identityBody(terms: ServiceIdentityTerms) {
  return {
    display_name: terms.displayName.trim(),
    identity_kind: terms.identityKind,
    purpose: terms.purpose.trim(),
    owner_kind: terms.ownerKind,
    owner_id: terms.ownerId,
    review_by: terms.reviewBy,
    environment: optional(terms.environment) ?? null,
    expires_on: optional(terms.expiresOn),
  };
}

export async function recordServiceIdentity(terms: ServiceIdentityTerms): Promise<string> {
  const tenantId = requireActiveTenantId();
  const result = await client.recordServiceIdentity({ params: { tenant_id: tenantId }, body: identityBody(terms) });
  if (!result.ok) throw failure(result, 'record the service identity');
  if (!result.data) throw new WorkforceRequestError('The service identity was not recorded.', null, false);
  return result.data.service_identity_id;
}

export async function reviseServiceIdentity(
  serviceIdentityId: string,
  expectedRevision: number,
  terms: ServiceIdentityTerms,
  lifecycleStatus: string
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.reviseServiceIdentity({
    params: { tenant_id: tenantId, service_identity_id: serviceIdentityId },
    body: { expected_revision: expectedRevision, lifecycle_status: lifecycleStatus, ...identityBody(terms) },
  });
  if (!result.ok) throw failure(result, 'save this service identity');
}

type RawResolution = { resolution: string; note: string; resolved_by: Actor; resolved_at: string } | null | undefined;

function resolutionOf(raw: RawResolution): ObservationResolution | null {
  return raw
    ? { resolution: raw.resolution, note: raw.note, resolvedBy: raw.resolved_by.display, resolvedAt: raw.resolved_at }
    : null;
}

// Closes a joiner/mover/leaver or reconciliation observation with an attributed note. Closing never
// grants, changes, or revokes anyone's access.
export async function resolveWorkforceObservation(
  observationId: string,
  resolution: 'resolved' | 'dismissed',
  note: string
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.resolveWorkforceObservation({
    params: { tenant_id: tenantId, observation_id: observationId },
    body: { resolution, note: note.trim() },
  });
  if (!result.ok) throw failure(result, 'close this observation');
}

export async function listReconciliationObservations(
  kind: string | null,
  status: string | null
): Promise<ReconciliationObservation[]> {
  const tenantId = requireActiveTenantId();
  const observations: ReconciliationObservation[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listWorkforceReconciliationObservations({
      params: { tenant_id: tenantId },
      query: { cursor, kind: kind ?? undefined, status: status ?? undefined },
    });
    if (!result.ok) throw failure(result, 'load the roster reconciliation');
    for (const item of result.data?.items ?? []) {
      if (item) {
        observations.push({
          observationId: item.observation_id,
          kind: item.kind,
          reason: item.reason,
          status: item.status,
          personIds: item.person_ids,
          relationshipIds: item.relationship_ids,
          userIds: item.user_ids,
          resolution: resolutionOf(item.resolution),
        });
      }
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return observations;
}

// Links a roster person to the platform member who is that person; no user clears the link.
export async function correlatePersonMembership(
  personId: string,
  expectedRevision: number,
  userId: string | null
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.correlatePersonMembership({
    params: { tenant_id: tenantId, person_id: personId },
    body: { expected_revision: expectedRevision, ...(userId ? { user_id: userId } : {}) },
  });
  if (!result.ok) throw failure(result, 'correlate this person with a member');
}

type RawSnapshot = {
  snapshot_id: string;
  root_snapshot_id: string;
  amends_snapshot_id: string | null;
  content_sha256: string;
  row_count: number | string;
  amendment_reason: string | null;
  frozen_by: Actor;
  frozen_at: string;
};

function snapshotSummaryOf(raw: RawSnapshot) {
  return {
    snapshotId: raw.snapshot_id,
    rootSnapshotId: raw.root_snapshot_id,
    amendsSnapshotId: raw.amends_snapshot_id,
    rowCount: Number(raw.row_count),
    contentSha256: raw.content_sha256,
    amendmentReason: raw.amendment_reason,
    frozenBy: raw.frozen_by.display,
    frozenAt: raw.frozen_at,
  };
}

type RawSnapshotDetail = RawSnapshot & {
  people: ({ person_id: string; revision: number | string; display_name: string; work_email: string | null } | null)[];
  work_relationships: ({
    relationship_id: string;
    revision: number | string;
    person_id: string;
    source_worker_id: string;
    worker_type: string;
    lifecycle_status: string;
    start_date: string;
    end_date: string | null;
    department: string | null;
    manager_person_id: string | null;
    sponsor_person_id: string | null;
  } | null)[];
  restricted_fields_redacted: boolean;
};

function snapshotOf(raw: RawSnapshotDetail): RosterSnapshot {
  return {
    ...snapshotSummaryOf(raw),
    people: raw.people
      .filter((item) => item !== null)
      .map((item) => ({
        personId: item.person_id,
        revision: Number(item.revision),
        displayName: item.display_name,
        workEmail: item.work_email,
      })),
    relationships: raw.work_relationships
      .filter((item) => item !== null)
      .map((item) => ({
        relationshipId: item.relationship_id,
        revision: Number(item.revision),
        personId: item.person_id,
        sourceWorkerId: item.source_worker_id,
        workerType: item.worker_type,
        lifecycleStatus: item.lifecycle_status,
        startDate: item.start_date,
        endDate: item.end_date,
        department: item.department,
        managerPersonId: item.manager_person_id,
        sponsorPersonId: item.sponsor_person_id,
      })),
    restrictedFieldsRedacted: raw.restricted_fields_redacted,
  };
}

export async function listRosterSnapshots(): Promise<RosterSnapshotSummary[]> {
  const tenantId = requireActiveTenantId();
  const snapshots: RosterSnapshotSummary[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listWorkforceRosterSnapshots({ params: { tenant_id: tenantId }, query: { cursor } });
    if (!result.ok) throw failure(result, 'load the roster snapshots');
    for (const item of result.data?.items ?? []) {
      if (item) snapshots.push({ ...snapshotSummaryOf(item), kind: item.kind });
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return snapshots;
}

export async function getRosterSnapshot(snapshotId: string): Promise<RosterSnapshot> {
  const tenantId = requireActiveTenantId();
  const result = await client.getWorkforceRosterSnapshot({ params: { tenant_id: tenantId, snapshot_id: snapshotId } });
  if (!result.ok) throw failure(result, 'load this roster snapshot');
  if (!result.data) throw new WorkforceRequestError('This roster snapshot was not found.', 404, false);
  return snapshotOf(result.data);
}

// The snapshot in force at a moment: the latest amendment frozen at or before it.
export async function getRosterSnapshotAsOf(asOf: string): Promise<RosterSnapshot> {
  const tenantId = requireActiveTenantId();
  const result = await client.getWorkforceRosterSnapshotAsOf({ params: { tenant_id: tenantId }, query: { as_of: asOf } });
  if (!result.ok) throw failure(result, 'load the roster snapshot for that time');
  if (!result.data) throw new WorkforceRequestError('No roster snapshot existed at that time.', 404, false);
  return snapshotOf(result.data);
}

export async function freezeRosterSnapshot(): Promise<string> {
  const tenantId = requireActiveTenantId();
  const result = await client.freezeWorkforceRosterSnapshot({ params: { tenant_id: tenantId } });
  if (!result.ok) throw failure(result, 'freeze the roster');
  if (!result.data) throw new WorkforceRequestError('The roster was not frozen.', null, false);
  return result.data.snapshot_id;
}

export async function amendRosterSnapshot(snapshotId: string, reason: string): Promise<string> {
  const tenantId = requireActiveTenantId();
  const result = await client.amendWorkforceRosterSnapshot({
    params: { tenant_id: tenantId, snapshot_id: snapshotId },
    body: { reason: reason.trim() },
  });
  if (!result.ok) throw failure(result, 'amend this roster snapshot');
  if (!result.data) throw new WorkforceRequestError('The amendment was not recorded.', null, false);
  return result.data.snapshot_id;
}

function failure(result: { ok: false; kind: string; status: number; error?: unknown }, action: string) {
  const problem =
    result.kind === 'http' && typeof result.error === 'object' && result.error !== null
      ? (result.error as { detail?: unknown; transient?: unknown })
      : {};
  const transient = problem.transient === true;
  if (result.status === 403) {
    return new WorkforceRequestError(`You do not have permission to ${action}.`, 403, false);
  }
  if (transient) {
    return new WorkforceRequestError(
      'The latest changes are still being processed. Try again in a moment.',
      result.status,
      true
    );
  }
  return new WorkforceRequestError(
    typeof problem.detail === 'string' && problem.detail.length > 0 ? problem.detail : `Unable to ${action}.`,
    result.kind === 'http' ? result.status : null,
    false
  );
}

export function isStaleConflict(error: unknown): boolean {
  return error instanceof WorkforceRequestError && error.status === 409 && !error.transient;
}
