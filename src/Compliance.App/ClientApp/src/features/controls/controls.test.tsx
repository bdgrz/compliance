// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { ControlDetailPage } from './pages/control-detail.js';
import { ControlsPage } from './pages/controls-list.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const programId = '0190a1b2-0000-7000-8000-0000000000b1';
const controlId = '0190a1b2-0000-7000-8000-0000000000c1';
const base = `/api/v1/tenants/${tenantId}`;
const controls = `${base}/programs/${programId}/controls`;
const draft = `${controls}/${controlId}/draft`;
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

const content = {
  title: 'Access reviews',
  objective: 'Access stays appropriate.',
  description: 'Quarterly access review of production systems.',
  implementation_narrative: 'The platform lead reviews access every quarter.',
  expected_evidence_descriptions: ['Signed quarterly review'],
  owner_reference: 'Platform lead',
  applicability: [
    { entry_id: '0190a1b2-0000-7000-8000-0000000000e1', subject_type: 'process', subject: 'Onboarding', governed_record_id: null, rationale: 'Grants access.', unresolved: true },
  ],
};

function draftBody(revision = 2) {
  return {
    tenant_id: tenantId,
    program_id: programId,
    control_id: controlId,
    identifier: 'AC-01',
    revision,
    status: 'draft',
    owner_resolution: 'unresolved',
    applicability_resolution: 'unresolved',
    content,
    last_changed_by_member_id: 'm',
    last_changed_by_display: 'Casey Lead',
    last_changed_at: '2026-09-20T00:00:00Z',
  };
}

function detailAnswers(revision = 2) {
  api.reply(`GET ${draft}`, 200, draftBody(revision));
  api.reply(`GET ${draft}/revisions`, 200, {
    items: [1, 2].map((r) => ({
      tenant_id: tenantId,
      program_id: programId,
      control_id: controlId,
      identifier: 'AC-01',
      revision: r,
      content,
      changed_by_member_id: 'm',
      changed_by_display: 'Casey Lead',
      changed_at: '2026-09-20T00:00:00Z',
    })),
    next_cursor: null,
  });
}

function field(container: HTMLElement, label: string) {
  return [...container.querySelectorAll('label')].find((l) => l.textContent?.startsWith(label))!
    .querySelector('input, textarea') as HTMLInputElement | HTMLTextAreaElement;
}

function type(element: HTMLInputElement | HTMLTextAreaElement, value: string) {
  element.value = value;
  element.dispatchEvent(new Event('input', { bubbles: true }));
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

describe('control drafts (R1-05 frontend #204)', () => {
  it('ShouldListControlDraftsWithLinks', async () => {
    // Arrange
    api.reply(`GET ${controls}`, 200, { items: [draftBody()], next_cursor: null });

    // Act
    const container = mount(() => <ControlsPage programId={programId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Access reviews'));

    // Assert
    expect(container.querySelector(`a[href="/acme/programs/${programId}/controls/${controlId}"]`)).not.toBeNull();
    expect(container.textContent).toContain('revision 2');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldInviteDraftingGivenNoControls', async () => {
    // Arrange
    api.reply(`GET ${controls}`, 200, { items: [], next_cursor: null });

    // Act
    const container = mount(() => <ControlsPage programId={programId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('No controls yet'));

    // Assert
    expect(field(container, 'Identifier')).not.toBeUndefined();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldCreateADraftWithEvidenceLines', async () => {
    // Arrange
    api.reply(`GET ${controls}`, 200, { items: [], next_cursor: null });
    api.reply(`POST ${controls}`, 409, problem(409, 'A control with this identifier already exists.'));
    const container = mount(() => <ControlsPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());
    type(field(container, 'Identifier'), 'AC-01');
    type(field(container, 'Title'), 'Access reviews');
    type(field(container, 'Objective'), 'Access stays appropriate.');
    type(field(container, 'Description'), 'Quarterly review.');
    type(field(container, 'Implementation narrative'), 'The lead reviews access.');
    type(field(container, 'Expected evidence'), 'Signed review\n\n Ticket export ');

    // Act
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    const sent = api.bodies.find((b) => b.method === 'POST' && b.path === controls)?.body as {
      identifier: string;
      content: { expected_evidence_descriptions: string[]; owner_reference: string | null };
    };
    expect(sent.identifier).toBe('AC-01');
    expect(sent.content.expected_evidence_descriptions).toEqual(['Signed review', 'Ticket export']);
    expect(sent.content.owner_reference).toBeNull();
  });

  it('ShouldShowForbiddenGivenTheViewerCannotReadControls', async () => {
    // Arrange
    api.reply(`GET ${controls}`, 403, problem(403, 'Forbidden'));

    // Act
    const container = mount(() => <ControlsPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Controls are not available to you'));

    // Assert
    expect(container.querySelector('form')).toBeNull();
  });

  it('ShouldShowDraftApplicabilityAndHistory', async () => {
    // Arrange
    detailAnswers();

    // Act
    const container = mount(() => <ControlDetailPage programId={programId} controlId={controlId} />);
    await vi.waitFor(() => expect(container.querySelectorAll('.control-history li')).toHaveLength(2));

    // Assert
    expect(container.querySelector('h1')?.textContent).toBe('AC-01 Access reviews');
    expect(container.querySelector('.control-history li')?.textContent).toContain('Revision 2');
    expect(container.textContent).toContain('not yet linked to a governed record');
    expect(field(container, 'Expected evidence').value).toBe('Signed quarterly review');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldReviseTheDraftAgainstTheExpectedRevision', async () => {
    // Arrange
    detailAnswers(2);
    api.reply(`PUT ${draft}`, 204);
    const container = mount(() => <ControlDetailPage programId={programId} controlId={controlId} />);
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());
    type(field(container, 'Title'), 'Quarterly access reviews');

    // Act
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(api.bodies.some((b) => b.method === 'PUT')).toBe(true));

    // Assert
    const sent = api.bodies.find((b) => b.method === 'PUT' && b.path === draft)?.body as {
      expected_revision: number;
      content: { title: string; applicability: unknown[] };
    };
    expect(sent.expected_revision).toBe(2);
    expect(sent.content.title).toBe('Quarterly access reviews');
    expect(sent.content.applicability).toEqual(content.applicability);
  });

  it('ShouldAskToReloadGivenAStaleDraftRevision', async () => {
    // Arrange
    detailAnswers(2);
    api.reply(`PUT ${draft}`, 409, problem(409, 'The control draft revision is stale; current revision is 3.'));
    const container = mount(() => <ControlDetailPage programId={programId} controlId={controlId} />);
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());

    // Act
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('Someone else changed this control');
  });

  it('ShouldExplainProjectionLagGivenATransientConflict', async () => {
    // Arrange
    api.reply(`GET ${draft}`, 409, problem(409, 'Projection is behind.', true));

    // Act
    const container = mount(() => <ControlDetailPage programId={programId} controlId={controlId} />);
    await vi.waitFor(() => expect(container.querySelector('h1')).not.toBeNull());

    // Assert
    expect(container.textContent).toContain('still being processed');
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Try again')).toBe(true);
  });

  it('ShouldShowNotFoundGivenAMissingControl', async () => {
    // Arrange
    api.reply(`GET ${draft}`, 404, problem(404, 'The control draft was not found.'));

    // Act
    const container = mount(() => <ControlDetailPage programId={programId} controlId={controlId} />);

    // Assert
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Control not found'));
  });
});
