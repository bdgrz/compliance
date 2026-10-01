// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { ApplicationDetailPage } from './pages/application-detail.js';
import { ApplicationsPage } from './pages/applications-list.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const applicationId = '0190a1b2-0000-7000-8000-0000000000a1';
const successorId = '0190a1b2-0000-7000-8000-0000000000a2';
const instanceId = '0190a1b2-0000-7000-8000-0000000000c1';
const alexId = '0190a1b2-0000-7000-8000-0000000000f1';
const blairId = '0190a1b2-0000-7000-8000-0000000000f2';
const base = `/api/v1/tenants/${tenantId}`;
const app = `${base}/applications/${applicationId}`;
const instancePath = `${app}/system-instances/${instanceId}`;
const actor = { kind: 'member', id: 'm', display: 'Casey Lead' };
let api: ReturnType<typeof stubApi>;
const mounted: RenderResult[] = [];

function mount(component: () => unknown): HTMLElement {
  const result = render(component as never);
  mounted.push(result);
  return result.container;
}

function applicationBody(id: string, name: string, extra: Record<string, unknown> = {}) {
  return {
    tenant_id: tenantId,
    application_id: id,
    revision: 2,
    name,
    purpose: 'Runs payroll',
    owner_reference: 'Finance',
    source_kind: 'manual',
    source_identifier: 'manual',
    has_system_instances: true,
    unresolved: [],
    last_changed_by_member_id: 'm',
    last_changed_by_display: 'Casey Lead',
    last_changed_at: '2026-09-20T00:00:00Z',
    classification: 'confidential',
    lifecycle: 'active',
    retirement: null,
    ...extra,
  };
}

function person(personId: string, name: string) {
  return {
    tenant_id: tenantId,
    person_id: personId,
    revision: 1,
    display_name: name,
    work_email: null,
    source_kind: 'manual',
    last_changed_by: actor,
    last_changed_at: '2026-09-20T00:00:00Z',
  };
}

const instance = {
  tenant_id: tenantId,
  application_id: applicationId,
  system_instance_id: instanceId,
  name: 'Payroll production',
  kind: 'production tenant',
  access_boundary_reference: null,
  source_kind: 'manual',
  source_identifier: null,
  unresolved: [],
  declared_by_member_id: 'm2',
  declared_by_display: 'Riley Admin',
  declared_at: '2026-09-21T00:00:00Z',
  revision: 1,
  lifecycle: 'active',
  retirement: null,
};

function answers(application = applicationBody(applicationId, 'Payroll')) {
  api.reply(`GET ${app}`, 200, application);
  api.reply(`${app}/system-instances`, 200, { items: [instance], next_cursor: null });
  api.reply(`${app}/revisions`, 200, { items: [], next_cursor: null });
  api.reply(`GET ${base}/applications`, 200, {
    items: [application, applicationBody(successorId, 'Workday')],
    next_cursor: null,
  });
  api.reply(`GET ${base}/people`, 200, { items: [person(alexId, 'Alex Rivera'), person(blairId, 'Blair Chen')], next_cursor: null });
}

function field(container: Element, label: string) {
  return [...container.querySelectorAll('label')].find((l) => l.textContent?.startsWith(label))!.querySelector(
    'input, select, textarea'
  ) as HTMLInputElement | HTMLSelectElement;
}

function setField(container: Element, label: string, value: string) {
  const element = field(container, label);
  element.value = value;
  element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? 'change' : 'input', { bubbles: true }));
}

function submit(container: Element, label: string) {
  container.querySelector(`form[aria-label="${label}"]`)!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
}

function button(container: Element, text: string) {
  return [...container.querySelectorAll('button')].find((b) => b.textContent === text) as HTMLButtonElement | undefined;
}

beforeEach(async () => {
  clearActiveTenant();
  api = stubApi();
  api.reply('/api/v1/tenant-slugs/acme/mine', 200, { tenant_id: tenantId, current_slug: 'acme', redirect: false });
  api.reply('/api/v1/tenants/mine', 200, { items: [{ tenant_id: tenantId, name: 'Acme Corp', slug: 'acme' }], next_cursor: null });
  await resolveTenantRoute('acme', { pathname: '/acme/applications', search: '', hash: '' });
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('application owners and merge-into (R1-10 frontend b #485)', () => {
  it('ShouldDeclareOwnersChosenFromTheWorkforceRoster', async () => {
    // Arrange
    api.reply(`GET ${base}/applications`, 200, { items: [], next_cursor: null });
    api.reply(`GET ${base}/people`, 200, { items: [person(alexId, 'Alex Rivera'), person(blairId, 'Blair Chen')], next_cursor: null });
    api.reply(`POST ${base}/applications`, 200, { application_id: applicationId });
    const container = mount(ApplicationsPage);
    await vi.waitFor(() => expect(field(container, 'System owner').querySelectorAll('option')).toHaveLength(3));
    setField(container, 'Application name', 'Payroll');
    setField(container, 'Purpose', 'Runs payroll');
    setField(container, 'System owner', alexId);
    setField(container, 'Access owner', blairId);

    // Act
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(api.bodies.some((b) => b.method === 'POST' && b.path === `${base}/applications`)).toBe(true));

    // Assert
    const sent = api.bodies.find((b) => b.method === 'POST' && b.path === `${base}/applications`)?.body;
    expect(sent).toMatchObject({ system_owner_person_id: alexId, access_owner_person_id: blairId });
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldShowOwnerNamesAndKeepAnOwnerMissingFromTheRoster', async () => {
    // Arrange
    const unknownId = '0190a1b2-0000-7000-8000-0000000000ff';
    answers(applicationBody(applicationId, 'Payroll', { system_owner_person_id: alexId, access_owner_person_id: unknownId }));

    // Act
    const container = mount(() => <ApplicationDetailPage applicationId={applicationId} />);
    await vi.waitFor(() => expect(container.querySelector(`a[href="/acme/workforce/people/${alexId}"]`)).not.toBeNull());

    // Assert
    expect(container.querySelector('.application-facts')?.textContent).toContain('Person not on the roster');
    const revise = container.querySelector('form[aria-label="Revise application"]')!;
    expect((field(revise, 'Access owner') as HTMLSelectElement).value).toBe(unknownId);
  });

  it('ShouldRetireMergedIntoAnActiveApplication', async () => {
    // Arrange
    answers();
    api.reply(`POST ${app}/change-previews`, 200, {
      tenant_id: tenantId,
      application_id: applicationId,
      application_revision: 2,
      change_kind: 'retire',
      changes: [],
      boundary_references: [],
      pending_contexts: [],
      complete: true,
    });
    api.reply(`POST ${app}/retirements`, 204);
    const container = mount(() => <ApplicationDetailPage applicationId={applicationId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Workday'));
    const form = container.querySelector('form[aria-label="Retire application"]')!;
    const targets = [...field(form, 'Merged into').querySelectorAll('option')].map((o) => o.textContent);
    setField(form, 'Reason for retirement', 'Consolidated');
    setField(form, 'Merged into', successorId);

    // Act
    submit(container, 'Retire application');
    await vi.waitFor(() => expect(button(container, 'Retire application')).toBeDefined());
    button(container, 'Retire application')!.click();
    await vi.waitFor(() => expect(api.bodies.some((b) => b.path === `${app}/retirements`)).toBe(true));

    // Assert
    expect(targets).not.toContain('Payroll');
    expect(api.bodies.find((b) => b.path === `${app}/retirements`)?.body).toMatchObject({
      reason: 'Consolidated',
      merged_into_application_id: successorId,
    });
  });

  it('ShouldLinkTheSuccessorGivenAMergedApplication', async () => {
    // Arrange
    answers(
      applicationBody(applicationId, 'Payroll', {
        lifecycle: 'retired',
        retirement: { effective_at: '2026-10-01T00:00:00Z', reason: 'Consolidated', merged_into_application_id: successorId },
      })
    );

    // Act
    const container = mount(() => <ApplicationDetailPage applicationId={applicationId} />);
    await vi.waitFor(() => expect(container.querySelector(`a[href="/acme/applications/${successorId}"]`)?.textContent).toBe('Workday'));

    // Assert
    expect(container.querySelector('.application-retired')?.textContent).toContain('Merged into');
    expect(await accessibilityViolations(container)).toEqual([]);
  });
});

describe('system instance retirement preview (R1-10 frontend b #485)', () => {
  async function openInstance(container: HTMLElement) {
    await vi.waitFor(() => expect(container.textContent).toContain('Payroll production'));
    [...container.querySelectorAll('button')].find((b) => b.textContent?.startsWith('Access-review scope for'))!.click();
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Retire system instance"]')).not.toBeNull());
  }

  it('ShouldPreviewBoundaryImpactBeforeRetiringAnInstance', async () => {
    // Arrange
    answers();
    api.reply(`${instancePath}/access-review-scope`, 200, {
      tenant_id: tenantId,
      application_id: applicationId,
      system_instance_id: instanceId,
      status: 'unresolved',
      as_of: '2026-09-25T00:00:00Z',
      effective: null,
      decisions: [],
    });
    api.reply(`${instancePath}/boundary-references`, 200, {
      items: [
        {
          tenant_id: tenantId, subject_type: 'system_instance', governed_record_id: instanceId, boundary_id: 'b1', program_id: 'p1',
          version_id: 'v', entry_id: 'e1', revision: 1, status: 'approved', effective_from: null, kind: 'in_scope',
          subject: 'Payroll production', owner_reference: 'x', rationale: 'Holds payroll data.',
        },
      ],
      next_cursor: null,
    });
    api.reply(`POST ${instancePath}/retirements`, 204);
    const container = mount(() => <ApplicationDetailPage applicationId={applicationId} />);
    await openInstance(container);
    const form = container.querySelector('form[aria-label="Retire system instance"]')!;
    setField(form, 'Retirement reason', 'Decommissioned');

    // Act
    submit(container, 'Retire system instance');
    await vi.waitFor(() => expect(container.textContent).toContain('1 boundary entry still names'));

    // Assert
    expect(api.bodies.some((b) => b.path === `${instancePath}/retirements`)).toBe(false);
    expect(container.querySelector('a[href="/acme/programs/p1/boundaries/b1"]')).not.toBeNull();
    button(container, 'Retire system instance')!.click();
    await vi.waitFor(() => expect(api.bodies.some((b) => b.path === `${instancePath}/retirements`)).toBe(true));
    expect(api.bodies.find((b) => b.path === `${instancePath}/retirements`)?.body).toMatchObject({
      expected_revision: 1,
      reason: 'Decommissioned',
    });
  });

  it('ShouldExplainForbiddenGivenThePreviewIsDenied', async () => {
    // Arrange
    answers();
    api.reply(`${instancePath}/access-review-scope`, 200, {
      tenant_id: tenantId, application_id: applicationId, system_instance_id: instanceId, status: 'unresolved',
      as_of: '2026-09-25T00:00:00Z', effective: null, decisions: [],
    });
    api.reply(`${instancePath}/boundary-references`, 403, {
      type: 'about:blank', title: 'Forbidden', status: 403, detail: 'Forbidden', instance: '/',
    });
    const container = mount(() => <ApplicationDetailPage applicationId={applicationId} />);
    await openInstance(container);
    setField(container.querySelector('form[aria-label="Retire system instance"]')!, 'Retirement reason', 'Gone');

    // Act
    submit(container, 'Retire system instance');
    await vi.waitFor(() =>
      expect(container.querySelector('form[aria-label="Retire system instance"] [role="alert"]')).not.toBeNull()
    );

    // Assert
    expect(button(container, 'Retire system instance')).toBeUndefined();
    expect(api.bodies.some((b) => b.path === `${instancePath}/retirements`)).toBe(false);
  });
});
