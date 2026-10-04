import { createClient, defineApi, empty, get, json } from '@askrjs/fetch';

import { createApiClient } from '../../api-client/index.js';
import { requireActiveTenantId } from '../tenants/tenants.js';

const client = createApiClient();

interface ProviderCoverageGapResponse {
  gap_id: string;
  revision: number | string;
  content: {
    service: string;
    assertion: string;
    period_start: string;
    period_end: string;
    description: string;
  };
  status: string;
  redacted?: boolean;
  risk_acceptances?: ({
    risk_id: string;
    acceptance_id: string;
    expires_at: string;
  } | null)[] | null;
}

interface ProviderCoverageGapPage {
  items: (ProviderCoverageGapResponse | null)[] | null;
  next_cursor: string | null;
}

interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail: string;
  instance: string;
  transient?: boolean;
}

// The server already exposes this read route, but the checked-in generated API client predates
// it. Keep the local descriptor typed and use the same fetch contract until that client is refreshed.
const coverageGapClient = createClient(
  defineApi({
    listProviderCoverageGaps: get('/api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps')
      .params<{ tenant_id: string; provider_id: string }>({
        tenant_id: { style: 'simple', explode: false },
        provider_id: { style: 'simple', explode: false },
      })
      .query<{ cursor?: string; limit?: number }>({
        cursor: { style: 'form', explode: true },
        limit: { style: 'form', explode: true },
      })
      .returns(json<ProviderCoverageGapPage>())
      .errors({
        400: json<ProblemDetails>(),
        401: empty(),
        403: json<ProblemDetails>(),
        404: json<ProblemDetails>(),
        409: json<ProblemDetails>(),
        500: json<ProblemDetails>(),
      }),
  })
);

export type Materiality = 'material' | 'not_material';
export type MaterialityBasis = 'customer_data' | 'critical_path';
export type BoundaryTreatment = 'carve_out' | 'inclusive';
export type DependencyKind = 'client_service' | 'system_instance';
export type MetadataClassification = 'public' | 'internal';

// Ordinary citation metadata only: the register never holds or fetches the cited content.
export interface SourceCitation {
  artifactKind: string;
  title: string;
  versionOrDate: string;
  locator: string;
  metadataClassification: MetadataClassification;
  artifactId: string | null;
}

export interface ProviderDependency {
  subjectKind: DependencyKind;
  subjectId: string | null;
  programId: string | null;
  applicationId: string | null;
  rationale: string;
  effectiveFrom: string;
  effectiveUntilExclusive: string | null;
  unresolvedReference: string | null;
  sourceCitation: SourceCitation | null;
}

export interface ProviderContent {
  name: string;
  providerKind: string;
  materiality: Materiality | null;
  materialityBasis: MaterialityBasis[];
  materialityRationale: string | null;
  subservice: boolean;
  boundaryTreatment: BoundaryTreatment | null;
  boundaryTreatmentRationale: string | null;
  ownerPersonId: string | null;
  ownerReference: string | null;
  dependencies: ProviderDependency[];
  sourceCitation: SourceCitation | null;
}

export interface Provider {
  providerId: string;
  revision: number;
  content: ProviderContent;
  sourceKind: string;
  lifecycle: string;
  unresolved: string[];
  recordedBy: string;
  recordedAt: string;
}

export type ProviderChangeKind = 'renewal' | 'material_change' | 'termination';

export interface ProviderChangeAffectedRecord {
  recordType: string;
  recordId: string;
  parentRecordId: string | null;
  revision: number | null;
  relationship: string;
}

export interface ProviderChangeImpactSection {
  context: string;
  records: ProviderChangeAffectedRecord[];
  complete: boolean;
  incompleteReason: string | null;
}

export interface ProviderChangeImpactPreview {
  providerId: string;
  providerRevision: number;
  changeKind: ProviderChangeKind;
  effectiveOn: string;
  changeSummary: string;
  contexts: ProviderChangeImpactSection[];
  pendingContexts: string[];
  complete: boolean;
  digest: string;
}

export interface ClientServiceChoice {
  serviceId: string;
  programId: string | null;
  name: string;
}

export interface ProviderAssuranceReport {
  reportId: string;
  revision: number;
  providerRevision: number;
  reportKind: string;
  issuer: string;
  scope: string;
  coveredServices: string[];
  periodStart: string | null;
  periodEnd: string;
  opinion: string | null;
}

export interface ProviderReview {
  reviewId: string;
  reviewedAt: string;
  nextReviewDue: string;
  evidenceKind: string;
  conclusion: string;
  rationale: string;
  assuranceReportId: string | null;
  providerRevision: number;
  assuranceReportRevision: number | null;
  reviewedBy: string;
}

export interface ProviderReportCoverage {
  reportId: string;
  revision: number;
  currency: string;
  periodCoverage: string;
  complete: boolean;
  incompleteReasons: string[];
}

export interface ProviderAssuranceCoverage {
  providerRevision: number;
  asOf: string;
  status: string;
  complete: boolean;
  reasons: string[];
  latestReviewedAt: string | null;
  nextReviewDue: string | null;
  latestConclusion: string | null;
  reports: ProviderReportCoverage[];
}

export interface ProviderCoverageGap {
  gapId: string;
  revision: number;
  status: string;
  service: string;
  assertion: string;
  periodStart: string;
  periodEnd: string;
  description: string | null;
  redacted: boolean;
  riskAcceptances: ProviderCoverageGapRiskAcceptance[];
}

export interface ProviderCoverageGapRiskAcceptance {
  riskId: string;
  acceptanceId: string;
  expiresAt: string;
}

export interface ProviderAssuranceData {
  reports: ProviderAssuranceReport[];
  reviews: ProviderReview[];
  coverage: ProviderAssuranceCoverage;
  coverageGaps: ProviderCoverageGap[];
}

// Keeps the HTTP status and the server's transient flag so pages can tell forbidden, not found,
// a stale revision (409), and projection lag (a transient 409) apart.
export class ProviderRequestError extends Error {
  constructor(
    message: string,
    readonly status: number | null,
    readonly transient: boolean
  ) {
    super(message);
  }
}

const unresolvedLabels: Record<string, string> = {
  materiality: 'Materiality unresolved',
  owner: 'Owner unresolved',
  source_citation: 'Source citation unresolved',
  csocs: 'CSOCs unresolved',
};

export function unresolvedLabel(code: string): string {
  const dependency = /^dependencies\[(\d+)\]\.(subject|source_citation)$/.exec(code);
  if (dependency) {
    const what = dependency[2] === 'subject' ? 'subject' : 'source citation';
    return `Dependency ${Number(dependency[1]) + 1} ${what} unresolved`;
  }
  return unresolvedLabels[code] ?? `${code.replaceAll('_', ' ')} unresolved`;
}

const optionLabels: Record<string, string> = {
  material: 'Material',
  not_material: 'Not material',
  customer_data: 'Customer data',
  critical_path: 'Critical path',
  carve_out: 'Carved out',
  inclusive: 'Inclusive',
  public: 'Public',
  internal: 'Internal',
  client_service: 'Client service',
  system_instance: 'System instance',
};

export function optionLabel(value: string): string {
  return optionLabels[value] ?? value.charAt(0).toUpperCase() + value.slice(1).replaceAll('_', ' ');
}

type Raw<T> = T | null | undefined;
type RawCitation = Raw<{
  artifact_kind: string;
  title: string;
  version_or_date: string;
  locator: string;
  metadata_classification: string;
  artifact_id?: string | null;
}>;
type RawDependency = {
  subject_kind: string;
  subject_id: string | null;
  program_id: string | null;
  application_id: string | null;
  rationale: string;
  effective_from: string;
  effective_until_exclusive?: string | null;
  unresolved_reference?: string | null;
  source_citation?: RawCitation;
};
type RawView = {
  provider_id: string;
  revision: number | string;
  content: {
    name: string;
    provider_kind: string;
    materiality?: string | null;
    materiality_basis?: (string | null)[] | null;
    materiality_rationale?: string | null;
    subservice?: boolean;
    boundary_treatment?: string | null;
    boundary_treatment_rationale?: string | null;
    owner_person_id?: string | null;
    owner_reference?: string | null;
    dependencies?: (RawDependency | null)[] | null;
    source_citation?: RawCitation;
  };
  source_kind: string;
  lifecycle: string;
  unresolved: (string | null)[];
  recorded_by: { display: string };
  recorded_at: string;
};

function citationOf(raw: RawCitation): SourceCitation | null {
  return raw
    ? {
        artifactKind: raw.artifact_kind,
        title: raw.title,
        versionOrDate: raw.version_or_date,
        locator: raw.locator,
        metadataClassification: raw.metadata_classification === 'internal' ? 'internal' : 'public',
        artifactId: raw.artifact_id ?? null,
      }
    : null;
}

function citationBody(citation: SourceCitation | null) {
  return citation
    ? {
        artifact_kind: citation.artifactKind,
        title: citation.title,
        version_or_date: citation.versionOrDate,
        locator: citation.locator,
        metadata_classification: citation.metadataClassification,
        artifact_id: citation.artifactId,
      }
    : null;
}

function providerOf(raw: RawView): Provider {
  const content = raw.content;
  return {
    providerId: raw.provider_id,
    revision: Number(raw.revision),
    content: {
      name: content.name,
      providerKind: content.provider_kind,
      materiality: content.materiality === 'material' || content.materiality === 'not_material' ? content.materiality : null,
      materialityBasis: (content.materiality_basis ?? []).filter(
        (basis): basis is MaterialityBasis => basis === 'customer_data' || basis === 'critical_path'
      ),
      materialityRationale: content.materiality_rationale ?? null,
      subservice: content.subservice ?? false,
      boundaryTreatment:
        content.boundary_treatment === 'carve_out' || content.boundary_treatment === 'inclusive' ? content.boundary_treatment : null,
      boundaryTreatmentRationale: content.boundary_treatment_rationale ?? null,
      ownerPersonId: content.owner_person_id ?? null,
      ownerReference: content.owner_reference ?? null,
      dependencies: (content.dependencies ?? [])
        .filter((dependency): dependency is RawDependency => dependency !== null)
        .map((dependency) => ({
          subjectKind: dependency.subject_kind === 'system_instance' ? 'system_instance' : 'client_service',
          subjectId: dependency.subject_id,
          programId: dependency.program_id,
          applicationId: dependency.application_id,
          rationale: dependency.rationale,
          effectiveFrom: dependency.effective_from,
          effectiveUntilExclusive: dependency.effective_until_exclusive ?? null,
          unresolvedReference: dependency.unresolved_reference ?? null,
          sourceCitation: citationOf(dependency.source_citation),
        })),
      sourceCitation: citationOf(content.source_citation),
    },
    sourceKind: raw.source_kind,
    lifecycle: raw.lifecycle,
    unresolved: raw.unresolved.filter((code): code is string => code !== null),
    recordedBy: raw.recorded_by.display,
    recordedAt: raw.recorded_at,
  };
}

// Server-captured source revisions are deliberately not sent: the server records what it inspected.
function contentBody(content: ProviderContent) {
  return {
    name: content.name,
    provider_kind: content.providerKind,
    materiality: content.materiality,
    materiality_basis: content.materialityBasis,
    materiality_rationale: content.materialityRationale,
    subservice: content.subservice,
    boundary_treatment: content.subservice ? content.boundaryTreatment : null,
    boundary_treatment_rationale: content.subservice ? content.boundaryTreatmentRationale : null,
    owner_person_id: content.ownerPersonId,
    owner_reference: content.ownerReference,
    dependencies: content.dependencies.map((dependency) => ({
      subject_kind: dependency.subjectKind,
      subject_id: dependency.subjectId,
      program_id: dependency.subjectId ? dependency.programId : null,
      application_id: dependency.subjectId ? dependency.applicationId : null,
      rationale: dependency.rationale,
      effective_from: dependency.effectiveFrom,
      effective_until_exclusive: dependency.effectiveUntilExclusive,
      unresolved_reference: dependency.subjectId ? null : dependency.unresolvedReference,
      source_citation: citationBody(dependency.sourceCitation),
    })),
    source_citation: citationBody(content.sourceCitation),
  };
}

export async function listProviders(): Promise<Provider[]> {
  const tenantId = requireActiveTenantId();
  const providers: Provider[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listProviders({ params: { tenant_id: tenantId }, query: { cursor } });
    if (!result.ok) throw failure(result, 'load the provider register');
    for (const item of result.data?.items ?? []) {
      if (item) providers.push(providerOf(item as RawView));
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return providers;
}

export async function getProvider(providerId: string, minimumRevision?: number): Promise<Provider> {
  const tenantId = requireActiveTenantId();
  const result = await client.getProvider({
    params: { tenant_id: tenantId, provider_id: providerId },
    query: { minimum_revision: minimumRevision },
  });
  if (!result.ok) throw failure(result, 'load this provider');
  if (!result.data) throw new ProviderRequestError('This provider was not found.', 404, false);
  return providerOf(result.data as RawView);
}

export async function listProviderRevisions(providerId: string): Promise<Provider[]> {
  const tenantId = requireActiveTenantId();
  const revisions: Provider[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listProviderRevisions({
      params: { tenant_id: tenantId, provider_id: providerId },
      query: { cursor },
    });
    if (!result.ok) throw failure(result, 'load the provider history');
    for (const item of result.data?.items ?? []) {
      if (item) revisions.push(providerOf(item as RawView));
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return revisions.sort((a, b) => b.revision - a.revision);
}

async function listAssuranceReports(tenantId: string, providerId: string): Promise<ProviderAssuranceReport[]> {
  const reports: ProviderAssuranceReport[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listProviderAssuranceReports({
      params: { tenant_id: tenantId, provider_id: providerId },
      query: { cursor },
    });
    if (!result.ok) throw failure(result, 'load assurance reports');
    for (const item of result.data?.items ?? []) {
      if (!item) continue;
      reports.push({
        reportId: item.report_id,
        revision: Number(item.revision),
        providerRevision: Number(item.provider_revision),
        reportKind: item.content.report_kind,
        issuer: item.content.issuer,
        scope: item.content.scope,
        coveredServices: (item.content.covered_services ?? []).filter((service): service is string => service !== null),
        periodStart: item.content.period_start ?? null,
        periodEnd: item.content.period_end,
        opinion: item.content.opinion ?? null,
      });
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return reports;
}

async function listReviews(tenantId: string, providerId: string): Promise<ProviderReview[]> {
  const reviews: ProviderReview[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listProviderReviews({
      params: { tenant_id: tenantId, provider_id: providerId },
      query: { cursor },
    });
    if (!result.ok) throw failure(result, 'load due-diligence reviews');
    for (const item of result.data?.items ?? []) {
      if (!item) continue;
      reviews.push({
        reviewId: item.review_id,
        reviewedAt: item.content.reviewed_at,
        nextReviewDue: item.content.next_review_due,
        evidenceKind: item.content.evidence_kind,
        conclusion: item.content.conclusion,
        rationale: item.content.rationale,
        assuranceReportId: item.content.assurance_report_id ?? null,
        providerRevision: Number(item.provider_revision),
        assuranceReportRevision: item.assurance_report_revision === null ? null : Number(item.assurance_report_revision),
        reviewedBy: item.reviewed_by.display,
      });
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return reviews.sort((a, b) => b.reviewedAt.localeCompare(a.reviewedAt));
}

async function assuranceCoverage(tenantId: string, providerId: string): Promise<ProviderAssuranceCoverage> {
  const result = await client.getProviderAssuranceCoverage({ params: { tenant_id: tenantId, provider_id: providerId }, query: {} });
  if (!result.ok) throw failure(result, 'load assurance coverage');
  if (!result.data) throw new ProviderRequestError('The assurance coverage response was empty.', null, false);
  return {
    providerRevision: Number(result.data.provider_revision),
    asOf: result.data.as_of,
    status: result.data.status,
    complete: result.data.complete,
    reasons: (result.data.reasons ?? []).filter((reason): reason is string => reason !== null),
    latestReviewedAt: result.data.latest_reviewed_at,
    nextReviewDue: result.data.next_review_due,
    latestConclusion: result.data.latest_conclusion,
    reports: (result.data.reports ?? [])
      .filter((report): report is NonNullable<typeof report> => report !== null)
      .map((report) => ({
        reportId: report.report_id,
        revision: Number(report.revision),
        currency: report.currency,
        periodCoverage: report.period_coverage,
        complete: report.complete,
        incompleteReasons: (report.incomplete_reasons ?? []).filter((reason): reason is string => reason !== null),
      })),
  };
}

async function listCoverageGaps(tenantId: string, providerId: string): Promise<ProviderCoverageGap[]> {
  const gaps: ProviderCoverageGap[] = [];
  let cursor: string | undefined;
  do {
    const result = await coverageGapClient.listProviderCoverageGaps({
      params: { tenant_id: tenantId, provider_id: providerId },
      query: { cursor },
    });
    if (!result.ok) throw failure(result, 'load provider coverage gaps');
    for (const item of result.data?.items ?? []) {
      if (!item) continue;
      gaps.push({
        gapId: item.gap_id,
        revision: Number(item.revision),
        status: item.status,
        service: item.content.service,
        assertion: item.content.assertion,
        periodStart: item.content.period_start,
        periodEnd: item.content.period_end,
        description: item.redacted ? null : item.content.description,
        redacted: item.redacted ?? false,
        riskAcceptances: (item.risk_acceptances ?? [])
          .filter((acceptance): acceptance is NonNullable<typeof acceptance> => acceptance !== null)
          .map((acceptance) => ({
            riskId: acceptance.risk_id,
            acceptanceId: acceptance.acceptance_id,
            expiresAt: acceptance.expires_at,
          })),
      });
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return gaps;
}

export async function getProviderAssurance(providerId: string): Promise<ProviderAssuranceData> {
  const tenantId = requireActiveTenantId();
  const [reports, reviews, coverage, coverageGaps] = await Promise.all([
    listAssuranceReports(tenantId, providerId),
    listReviews(tenantId, providerId),
    assuranceCoverage(tenantId, providerId),
    listCoverageGaps(tenantId, providerId),
  ]);
  return { reports, reviews, coverage, coverageGaps };
}

export async function recordProvider(content: ProviderContent): Promise<string> {
  const tenantId = requireActiveTenantId();
  const result = await client.recordProvider({ params: { tenant_id: tenantId }, body: { content: contentBody(content) } });
  if (!result.ok) throw failure(result, 'record the provider');
  if (!result.data) throw new ProviderRequestError('The provider was not recorded.', null, false);
  return result.data.provider_id;
}

// Returns the revision the save produced so the page can read at least that revision back.
export async function reviseProvider(providerId: string, expectedRevision: number, content: ProviderContent): Promise<number> {
  const tenantId = requireActiveTenantId();
  const result = await client.reviseProvider({
    params: { tenant_id: tenantId, provider_id: providerId },
    body: { expected_revision: expectedRevision, content: contentBody(content) },
  });
  if (!result.ok) throw failure(result, 'save the provider');
  return result.data ? Number(result.data.revision) : expectedRevision + 1;
}

export async function previewProviderChange(
  providerId: string,
  expectedRevision: number,
  changeKind: ProviderChangeKind,
  effectiveOn: string,
  changeSummary: string
): Promise<ProviderChangeImpactPreview> {
  const tenantId = requireActiveTenantId();
  const result = await client.previewProviderChange({
    params: { tenant_id: tenantId, provider_id: providerId },
    body: {
      expected_revision: expectedRevision,
      change_kind: changeKind,
      effective_on: effectiveOn,
      change_summary: changeSummary,
    },
  });
  if (!result.ok) throw failure(result, 'preview this provider change');
  if (!result.data) throw new ProviderRequestError('The impact preview was empty.', null, false);

  return {
    providerId: result.data.provider_id,
    providerRevision: Number(result.data.provider_revision),
    changeKind: result.data.change_kind as ProviderChangeKind,
    effectiveOn: result.data.effective_on,
    changeSummary: result.data.change_summary,
    contexts: result.data.contexts.map((section) => ({
      context: section.context,
      complete: section.complete,
      incompleteReason: section.incomplete_reason,
      records: section.records.map((record) => ({
        recordType: record.record_type,
        recordId: record.record_id,
        parentRecordId: record.parent_record_id,
        revision: record.revision === null ? null : Number(record.revision),
        relationship: record.relationship,
      })),
    })),
    pendingContexts: result.data.pending_contexts,
    complete: result.data.complete,
    digest: result.data.digest,
  };
}

// Existing client services a dependency can reference, each with its owning program.
export async function listClientServiceChoices(): Promise<ClientServiceChoice[]> {
  const tenantId = requireActiveTenantId();
  const services: ClientServiceChoice[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listClientServices({ params: { tenant_id: tenantId }, query: { cursor } });
    if (!result.ok) throw failure(result, 'load the client services');
    for (const item of result.data?.items ?? []) {
      if (item) services.push({ serviceId: item.service_id, programId: item.program_id ?? null, name: item.name });
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return services;
}

function failure(result: { ok: false; kind: string; status: number; error?: unknown }, action: string) {
  const problem =
    result.kind === 'http' && typeof result.error === 'object' && result.error !== null
      ? (result.error as { detail?: unknown; transient?: unknown })
      : {};
  if (result.status === 403) return new ProviderRequestError(`You do not have permission to ${action}.`, 403, false);
  if (problem.transient === true) {
    return new ProviderRequestError('The latest changes are still being processed. Try again in a moment.', result.status, true);
  }
  const detail = typeof problem.detail === 'string' ? problem.detail : '';
  return new ProviderRequestError(
    detail.length > 0 ? detail : `Unable to ${action}.`,
    result.kind === 'http' ? result.status : null,
    false
  );
}

export function messageFor(error: Error, subject: string): string {
  if (!(error instanceof ProviderRequestError)) return error.message;
  if (error.status === 409 && !error.transient) {
    return `Someone else changed ${subject} since you opened it. Reload to see their changes, then try again. (${error.message})`;
  }
  return error.message;
}
