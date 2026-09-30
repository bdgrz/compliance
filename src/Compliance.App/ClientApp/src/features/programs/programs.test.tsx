// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { ProgramDetailPage } from './pages/program-detail.js';
import { ProgramsPage } from './pages/programs-list.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const programId = '0190a1b2-0000-7000-8000-0000000000b1';
const base = `/api/v1/tenants/${tenantId}`;
const program = `${base}/programs/${programId}`;
let api: ReturnType<typeof stubApi>;
const mounted: RenderResult[] = [];

const plan = {
  target_readiness_date: '2026-12-01',
  target_type_i_as_of_date: '2027-02-01',
  target_type_ii_start_date: null,
  target_type_ii_end_date: null,
  readiness_advisor: 'Northwind Advisory',
  audit_firm: null,
};

function mount(component: () => unknown): HTMLElement {
  const result = render(component as never);
  mounted.push(result);
  return result.container;
}

function problem(status: number, detail: string, transient = false) {
  return { type: 'about:blank', title: 'Problem', status, detail, instance: '/', transient };
}

function programAnswers(revision = 2) {
  api.reply(`GET ${program}`, 200, {
    tenant_id: tenantId,
    program_id: programId,
    name: 'SOC 2 2026',
    stage: 'readiness',
    next_stage: 'type_i',
    revision,
    plan,
    last_changed_by_member_id: 'm',
    last_changed_by_display: 'Casey Lead',
    last_changed_at: '2026-09-20T00:00:00Z',
    stage_plan: [
      { stage: 'readiness', advance_when: 'Readiness assessment accepted by management.' },
      { stage: 'type_i', advance_when: 'Type I report issued.' },
      { stage: 'type_ii', advance_when: 'Type II period examined.' },
    ],
  });
  api.reply(`${program}/setup-work`, 200, {
    tenant_id: tenantId,
    program_id: programId,
    program_revision: revision,
    items: [{ code: 'boundary_missing', detail: 'Define the system boundary.', source_type: 'program', source_id: programId }],
    next_boundary_cursor: null,
  });
  api.reply(`${program}/revisions`, 200, {
    items: [
      { program_id: programId, revision: 1, name: 'SOC 2 2026', plan: { ...plan, readiness_advisor: null }, actor_member_id: 'm', actor_display: 'Casey Lead', changed_at: '2026-09-10T00:00:00Z' },
      { program_id: programId, revision: 2, name: 'SOC 2 2026', plan, actor_member_id: 'm', actor_display: 'Casey Lead', changed_at: '2026-09-20T00:00:00Z' },
    ],
    next_cursor: null,
  });
}

beforeEach(async () => {
  clearActiveTenant();
  api = stubApi();
  api.reply('/api/v1/tenant-slugs/acme/mine', 200, { tenant_id: tenantId, current_slug: 'acme', redirect: false });
  api.reply('/api/v1/tenants/mine', 200, {
    items: [{ tenant_id: tenantId, name: 'Acme Corp', slug: 'acme' }],
    next_cursor: null,
  });
  await resolveTenantRoute('acme', { pathname: '/acme/programs', search: '', hash: '' });
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('program plan and setup work (R1-01 frontend #177)', () => {
  it('ShouldShowStagePlanSetupWorkAndUnconfirmedTargets', async () => {
    // Arrange
    programAnswers();

    // Act
    const container = mount(() => <ProgramDetailPage programId={programId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Define the system boundary.'));

    // Assert
    expect(container.querySelector('h1')?.textContent).toBe('SOC 2 2026');
    expect(container.textContent).toContain('Current stage: Readiness · next: SOC 2 Type I');
    expect(container.querySelector('[aria-current="step"]')?.textContent).toContain('Readiness');
    expect(container.textContent).toContain('not dates confirmed by an audit firm');
    expect((container.querySelector('input[type="date"]') as HTMLInputElement).value).toBe('2026-12-01');
    await vi.waitFor(() => expect(container.querySelectorAll('.program-history li')).toHaveLength(2));
    expect(container.querySelector('.program-history li')?.textContent).toContain('Revision 2');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldReviseThePlanAgainstTheExpectedRevision', async () => {
    // Arrange
    programAnswers(2);
    api.reply(`PUT ${program}`, 204);
    const container = mount(() => <ProgramDetailPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());
    const auditFirm = [...container.querySelectorAll('label')].find((l) => l.textContent?.startsWith('Audit firm'))!
      .querySelector('input') as HTMLInputElement;
    auditFirm.value = 'Contoso CPAs';
    auditFirm.dispatchEvent(new Event('input', { bubbles: true }));

    // Act
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    const sent = api.bodies.find((b) => b.method === 'PUT' && b.path === program)?.body as Record<string, unknown>;
    expect(sent.expected_revision).toBe(2);
    expect(sent.plan).toEqual({ ...plan, audit_firm: 'Contoso CPAs' });
  });

  it('ShouldAskToReloadGivenAStaleRevisionConflict', async () => {
    // Arrange
    programAnswers(2);
    api.reply(`PUT ${program}`, 409, problem(409, 'The program revision is stale; current revision is 3.'));
    const container = mount(() => <ProgramDetailPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());

    // Act
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('Someone else changed this program');
  });

  it('ShouldExplainProjectionLagGivenATransientConflict', async () => {
    // Arrange
    api.reply(`GET ${program}`, 409, problem(409, 'Projection is behind.', true));

    // Act
    const container = mount(() => <ProgramDetailPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('h1')).not.toBeNull());

    // Assert
    expect(container.textContent).toContain('still being processed');
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Try again')).toBe(true);
  });

  it('ShouldShowForbiddenGivenTheViewerCannotReadPrograms', async () => {
    // Arrange
    api.reply(`${base}/programs`, 403, problem(403, 'Forbidden'));

    // Act
    const container = mount(ProgramsPage);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Programs are not available to you'));

    // Assert
    expect(container.querySelector('form')).toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldListProgramsWithTheirStages', async () => {
    // Arrange
    api.reply(`${base}/programs`, 200, {
      items: [{ tenant_id: tenantId, program_id: programId, name: 'SOC 2 2026', stage: 'readiness', next_stage: 'type_i', revision: 1, plan }],
      next_cursor: null,
    });

    // Act
    const container = mount(ProgramsPage);
    await vi.waitFor(() => expect(container.textContent).toContain('SOC 2 2026'));

    // Assert
    expect(container.querySelector(`a[href="/acme/programs/${programId}"]`)).not.toBeNull();
    expect(container.textContent).toContain('Readiness → SOC 2 Type I');
  });

  it('ShouldInviteStartingAProgramGivenNone', async () => {
    // Arrange
    api.reply(`${base}/programs`, 200, { items: [], next_cursor: null });

    // Act
    const container = mount(ProgramsPage);
    await vi.waitFor(() => expect(container.textContent).toContain('No programs yet'));

    // Assert
    expect(container.querySelector('form input')).not.toBeNull();
  });
});
