import { createApiClient } from '../../api-client/index.js';
import { programFailure, ProgramRequestError } from '../programs/programs.js';
import { requireActiveTenantId } from '../tenants/tenants.js';

const client = createApiClient();

export interface SupportGap {
  category: string;
  code: string;
  note: string;
}

export interface CriteriaEdition {
  editionId: string;
  framework: string;
  editionLabel: string;
  publishedAt: string;
  isComplete: boolean;
  coverageNote: string;
  sourceUrl: string;
  contentRights: string;
  supportGaps: SupportGap[];
}

export interface CriteriaEntry {
  identifier: string;
  sourceIdentifier: string | null;
  category: string;
  kind: string;
  parentIdentifier: string | null;
  summary: string;
}

export interface CriteriaFilter {
  category?: string;
  kind?: string;
}

// SOC 2 trust services categories. Security is required in every boundary (M0-D01); the others
// are optional selections.
export const criteriaCategories: { code: string; label: string; required: boolean }[] = [
  { code: 'security', label: 'Security', required: true },
  { code: 'availability', label: 'Availability', required: false },
  { code: 'processing_integrity', label: 'Processing integrity', required: false },
  { code: 'confidentiality', label: 'Confidentiality', required: false },
  { code: 'privacy', label: 'Privacy', required: false },
];

export function categoryLabel(code: string): string {
  return criteriaCategories.find((category) => category.code === code)?.label ?? code.replaceAll('_', ' ');
}

type EditionData = {
  edition_id: string;
  framework: string;
  edition_label: string;
  published_at: string;
  is_complete: boolean;
  coverage_note: string;
  source_url: string;
  content_rights: string;
  support_gaps: ({ category: string; code: string; note: string } | null)[];
};

function toEdition(data: EditionData): CriteriaEdition {
  return {
    editionId: data.edition_id,
    framework: data.framework,
    editionLabel: data.edition_label,
    publishedAt: data.published_at,
    isComplete: data.is_complete,
    coverageNote: data.coverage_note,
    sourceUrl: data.source_url,
    contentRights: data.content_rights,
    supportGaps: data.support_gaps.filter((gap) => gap !== null),
  };
}

export async function listCriteriaEditions(): Promise<CriteriaEdition[]> {
  const tenantId = requireActiveTenantId();
  const result = await client.listCriteriaCatalogEditions({ params: { tenant_id: tenantId } });
  if (!result.ok) throw programFailure(result, 'load the criteria catalogs');
  return (result.data ?? []).filter((item) => item !== null).map(toEdition);
}

export async function listCriteriaEntries(editionId: string, filter: CriteriaFilter = {}): Promise<CriteriaEntry[]> {
  const tenantId = requireActiveTenantId();
  const entries: CriteriaEntry[] = [];
  let cursor: string | undefined;
  do {
    const result = await client.listCriteriaCatalogEntries({
      params: { tenant_id: tenantId, edition_id: editionId },
      query: { category: filter.category, kind: filter.kind, cursor },
    });
    if (!result.ok) throw programFailure(result, 'load the criteria');
    for (const item of result.data?.items ?? []) {
      if (item) {
        entries.push({
          identifier: item.identifier,
          sourceIdentifier: item.source_identifier,
          category: item.category,
          kind: item.kind,
          parentIdentifier: item.parent_identifier,
          summary: item.summary,
        });
      }
    }
    cursor = result.data?.next_cursor ?? undefined;
  } while (cursor !== undefined);
  return entries;
}

export async function selectCriteriaEdition(
  programId: string,
  expectedRevision: number,
  editionId: string
): Promise<void> {
  const tenantId = requireActiveTenantId();
  const result = await client.selectProgramCriteriaEdition({
    params: { tenant_id: tenantId, program_id: programId },
    body: { expected_revision: expectedRevision, edition_id: editionId },
  });
  if (!result.ok) throw programFailure(result, 'select the criteria catalog');
}

export { ProgramRequestError };
