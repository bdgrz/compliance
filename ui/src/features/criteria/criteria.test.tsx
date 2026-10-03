// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { ProgramDetailPage } from '../programs/pages/program-detail.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const programId = '0190a1b2-0000-7000-8000-0000000000b1';
const editionId = 'aicpa-tsc-2017-rev-2022';
const base = `/api/v1/tenants/${tenantId}`;
const program = `${base}/programs/${programId}`;
let api: ReturnType<typeof stubApi>;
const mounted: RenderResult[] = [];

function mount(component: () => unknown): HTMLElement {
  const result = render(component as never);
  mounted.push(result);
  return result.container;
}

function problem(status: number, detail: string, transient = false) {
  return { type: 'about:blank', title: 'Problem', status, detail, instance: '/', transient };
}

const edition = {
  edition_id: editionId,
  framework: 'AICPA Trust Services Criteria',
  edition_label: '2017 (revised 2022)',
  published_at: '2022-10-01T00:00:00Z',
  is_complete: false,
  coverage_note: 'Privacy points of focus are summarized.',
  source_url: 'https://www.aicpa-cima.com/resources/tsc',
  content_rights: 'Summaries only; consult the AICPA publication for full text.',
  support_gaps: [{ category: 'privacy', code: 'privacy_lifecycle', note: 'Privacy lifecycle workflows are not yet supported.' }],
};

function programAnswers(criteriaEditionId: string | null = null) {
  api.reply(`GET ${program}`, 200, {
    tenant_id: tenantId,
    program_id: programId,
    name: 'SOC 2 2026',
    stage: 'readiness',
    next_stage: 'type_i',
    revision: 3,
    plan: {
      target_readiness_date: null,
      target_type_i_as_of_date: null,
      target_type_ii_start_date: null,
      target_type_ii_end_date: null,
      readiness_advisor: null,
      audit_firm: null,
    },
    last_changed_by_member_id: 'm',
    last_changed_by_display: 'Casey Lead',
    last_changed_at: '2026-09-20T00:00:00Z',
    stage_plan: [],
    criteria_edition_id: criteriaEditionId,
  });
  api.reply(`${program}/setup-work`, 200, { tenant_id: tenantId, program_id: programId, program_revision: 3, items: [], next_boundary_cursor: null });
  api.reply(`${program}/revisions`, 200, { items: [], next_cursor: null });
  api.reply(`GET ${base}/criteria-editions/${editionId}/entries`, 200, {
    items: [
      { edition_id: editionId, identifier: 'CC6.1', source_identifier: 'CC6.1', category: 'security', kind: 'criterion', parent_identifier: null, summary: 'Logical access security.' },
      { edition_id: editionId, identifier: 'CC6.1-POF1', source_identifier: null, category: 'security', kind: 'point_of_focus', parent_identifier: 'CC6.1', summary: 'Identifies and manages the inventory of information assets.' },
    ],
    next_cursor: null,
  });
}

beforeEach(async () => {
  clearActiveTenant();
  api = stubApi();
  api.reply('/api/v1/tenant-slugs/acme/mine', 200, { tenant_id: tenantId, current_slug: 'acme', redirect: false });
  api.reply('/api/v1/tenants/mine', 200, { items: [{ tenant_id: tenantId, name: 'Acme Corp', slug: 'acme' }], next_cursor: null });
  await resolveTenantRoute('acme', { pathname: '/acme/programs', search: '', hash: '' });
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('criteria catalog selection (R1-03 frontend #210)', () => {
  it('ShouldShowProvenanceGapsAndCriteriaGivenAnUnselectedProgram', async () => {
    // Arrange
    programAnswers(null);
    api.reply(`GET ${base}/criteria-editions`, 200, [edition]);

    // Act
    const container = mount(() => <ProgramDetailPage programId={programId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Logical access security.'));

    // Assert
    expect(container.textContent).toContain('No criteria catalog is selected');
    expect(container.textContent).toContain('Partial catalog.');
    expect(container.querySelector(`a[href="${edition.source_url}"]`)).not.toBeNull();
    expect(container.textContent).toContain('Security (required in every boundary)');
    expect(container.textContent).toContain('Privacy lifecycle workflows are not yet supported.');
    expect(container.textContent).toContain('Point of focus for CC6.1');
    expect(container.querySelector(`a[href="/acme/programs/${programId}/controls"]`)).not.toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldFilterCriteriaByCategory', async () => {
    // Arrange
    programAnswers(editionId);
    api.reply(`GET ${base}/criteria-editions`, 200, [edition]);
    const container = mount(() => <ProgramDetailPage programId={programId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Logical access security.'));
    const select = [...container.querySelectorAll('label')].find((l) => l.textContent?.startsWith('Category'))!
      .querySelector('select') as HTMLSelectElement;
    const entriesPath = `${base}/criteria-editions/${editionId}/entries`;
    const before = api.requested.filter((path) => path === entriesPath).length;

    // Act
    select.value = 'availability';
    select.dispatchEvent(new Event('change', { bubbles: true }));

    // Assert
    await vi.waitFor(() => expect(api.requested.filter((path) => path === entriesPath).length).toBe(before + 1));
    expect(select.value).toBe('availability');
    expect(container.textContent).toContain('This program traces to AICPA Trust Services Criteria');
  });

  it('ShouldSelectTheEditionAgainstTheProgramRevision', async () => {
    // Arrange
    programAnswers(null);
    api.reply(`GET ${base}/criteria-editions`, 200, [edition]);
    api.reply(`PUT ${program}/criteria-edition`, 204);
    const container = mount(() => <ProgramDetailPage programId={programId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Use this edition'));
    const form = [...container.querySelectorAll('form')].find((f) => f.textContent?.includes('Use this edition'))!;

    // Act
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(container.textContent).toContain('Criteria catalog selected.'));

    // Assert
    const sent = api.bodies.find((b) => b.method === 'PUT' && b.path === `${program}/criteria-edition`)?.body;
    expect(sent).toEqual({ expected_revision: 3, edition_id: editionId });
  });

  it('ShouldAskToReloadGivenAStaleProgramRevision', async () => {
    // Arrange
    programAnswers(null);
    api.reply(`GET ${base}/criteria-editions`, 200, [edition]);
    api.reply(`PUT ${program}/criteria-edition`, 409, problem(409, 'The program revision is stale.'));
    const container = mount(() => <ProgramDetailPage programId={programId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Use this edition'));
    const form = [...container.querySelectorAll('form')].find((f) => f.textContent?.includes('Use this edition'))!;

    // Act
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(container.textContent).toContain('Someone else changed this program'));

    // Assert
    expect(container.textContent).not.toContain('Criteria catalog selected.');
  });

  it('ShouldOfferRetryGivenTheCatalogFailsToLoad', async () => {
    // Arrange
    programAnswers(null);
    api.reply(`GET ${base}/criteria-editions`, 409, problem(409, 'Projection is behind.', true));

    // Act
    const container = mount(() => <ProgramDetailPage programId={programId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('still being processed'));

    // Assert
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Try again')).toBe(true);
  });

  it('ShouldExplainForbiddenGivenTheViewerCannotReadTheCatalog', async () => {
    // Arrange
    programAnswers(null);
    api.reply(`GET ${base}/criteria-editions`, 403, problem(403, 'Forbidden'));

    // Act
    const container = mount(() => <ProgramDetailPage programId={programId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('permission to view the criteria catalogs'));

    // Assert
    expect(container.textContent).not.toContain('Use this edition');
  });

  it('ShouldExplainGivenNoCatalogsExist', async () => {
    // Arrange
    programAnswers(null);
    api.reply(`GET ${base}/criteria-editions`, 200, []);

    // Act
    const container = mount(() => <ProgramDetailPage programId={programId} />);

    // Assert
    await vi.waitFor(() => expect(container.textContent).toContain('No criteria catalogs are available yet.'));
  });
});
