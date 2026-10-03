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
const instanceId = '0190a1b2-0000-7000-8000-0000000000c1';
const base = `/api/v1/tenants/${tenantId}`;
const app = `${base}/applications/${applicationId}`;
const instancePath = `${app}/system-instances/${instanceId}`;
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

function applicationBody(revision = 2, extra: Record<string, unknown> = {}) {
  return {
    tenant_id: tenantId,
    application_id: applicationId,
    revision,
    name: 'Payroll',
    purpose: 'Runs payroll',
    owner_reference: null,
    source_kind: 'manual',
    source_identifier: 'manual',
    has_system_instances: true,
    unresolved: ['owner_missing', 'classification_unresolved'],
    last_changed_by_member_id: 'm',
    last_changed_by_display: 'Casey Lead',
    last_changed_at: '2026-09-20T00:00:00Z',
    classification: null,
    lifecycle: 'active',
    retirement: null,
    ...extra,
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

function detailAnswers(revision = 2) {
  api.reply(`GET ${app}`, 200, applicationBody(revision));
  api.reply(`${app}/system-instances`, 200, { items: [instance], next_cursor: null });
  api.reply(`${app}/revisions`, 200, {
    items: [
      { ...applicationBody(1), change_kind: 'declared', system_instance_id: null, system_instance: null },
      { ...applicationBody(2), change_kind: 'system_instance_declared', system_instance_id: instanceId, system_instance: instance },
    ],
    next_cursor: null,
  });
}

const decision = {
  tenant_id: tenantId,
  application_id: applicationId,
  system_instance_id: instanceId,
  system_instance_revision: 1,
  decision_id: 'd1',
  sequence: 1,
  decision: 'included',
  reason: 'Holds payroll data',
  effective_from: '2026-09-22T00:00:00Z',
  review_by: null,
  approved_by: { kind: 'member', id: 'm3', display: 'Casey Lead' },
  decided_at: '2026-09-22T00:00:00Z',
  separation_of_duties_waiver_id: null,
};

function preview(kind: 'revise' | 'retire') {
  return {
    tenant_id: tenantId,
    application_id: applicationId,
    application_revision: 2,
    change_kind: kind,
    changes: kind === 'revise' ? [{ field: 'owner_reference', before: null, after: 'Finance' }] : [],
    boundary_references: [
      {
        tenant_id: tenantId, subject_type: 'application', governed_record_id: applicationId, boundary_id: 'b', program_id: 'p',
        version_id: 'v', entry_id: 'e1', revision: 1, status: 'approved', effective_from: null, kind: 'in_scope',
        subject: 'Payroll', owner_reference: 'x', rationale: 'Processes customer data.',
      },
    ],
    pending_contexts: ['vendors'],
    complete: false,
    system_instance_references: [instance],
  };
}

async function openScope(container: HTMLElement) {
  await vi.waitFor(() => expect(container.textContent).toContain('Payroll production'));
  const button = [...container.querySelectorAll('button')].find((b) => b.textContent?.startsWith('Access-review scope for'))!;
  button.click();
  await vi.waitFor(() => expect(container.querySelector('form[aria-label="Decide access-review scope"]')).not.toBeNull());
}

function submit(container: HTMLElement, label: string) {
  container.querySelector(`form[aria-label="${label}"]`)!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
}

function typeInto(container: HTMLElement, label: string, value: string) {
  const input = [...container.querySelectorAll('label')].find((l) => l.textContent?.startsWith(label))!
    .querySelector('input') as HTMLInputElement;
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
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

describe('applications and reviewed systems (R1-10a frontend #212)', () => {
  it('ShouldListApplicationsWithUnresolvedStates', async () => {
    // Arrange
    api.reply(`${base}/applications`, 200, { items: [applicationBody()], next_cursor: null });

    // Act
    const container = mount(ApplicationsPage);
    await vi.waitFor(() => expect(container.textContent).toContain('Payroll'));

    // Assert
    expect(container.querySelector(`a[href="/acme/applications/${applicationId}"]`)).not.toBeNull();
    expect(container.textContent).toContain('Owner missing');
    expect(container.textContent).toContain('Classification unresolved');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldInviteRecordingGivenNoApplications', async () => {
    // Arrange
    api.reply(`${base}/applications`, 200, { items: [], next_cursor: null });

    // Act
    const container = mount(ApplicationsPage);
    await vi.waitFor(() => expect(container.textContent).toContain('No applications yet'));

    // Assert
    expect(container.querySelector('form input')).not.toBeNull();
  });

  it('ShouldShowForbiddenGivenTheViewerCannotReadApplications', async () => {
    // Arrange
    api.reply(`${base}/applications`, 403, problem(403, 'Forbidden'));

    // Act
    const container = mount(ApplicationsPage);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Applications are not available to you'));

    // Assert
    expect(container.querySelector('form')).toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldOfferRetryGivenTheListFails', async () => {
    // Arrange
    api.reply(`${base}/applications`, 500, problem(500, 'Boom'));

    // Act
    const container = mount(ApplicationsPage);
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Try again')).toBe(true);
  });

  it('ShouldDeclareAnApplicationWithOwnersAndClassification', async () => {
    // Arrange
    api.reply(`${base}/applications`, 200, { items: [], next_cursor: null });
    api.reply(`POST ${base}/applications`, 400, problem(400, 'The name is required.'));
    const container = mount(ApplicationsPage);
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());
    typeInto(container, 'Application name', ' Payroll ');
    typeInto(container, 'Purpose', 'Runs payroll');
    typeInto(container, 'Data classification', 'confidential');

    // Act
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    const sent = api.bodies.find((b) => b.method === 'POST' && b.path === `${base}/applications`)?.body as Record<string, unknown>;
    expect(sent).toEqual({ name: 'Payroll', purpose: 'Runs payroll', owner_reference: null, classification: 'confidential' });
    expect(container.querySelector('[role="alert"]')?.textContent).toBe('The name is required.');
  });

  it('ShouldShowApplicationSystemsAndHistory', async () => {
    // Arrange
    detailAnswers();

    // Act
    const container = mount(() => <ApplicationDetailPage applicationId={applicationId} />);
    await vi.waitFor(() => expect(container.querySelectorAll('.application-history li')).toHaveLength(2));

    // Assert
    expect(container.querySelector('h1')?.textContent).toBe('Payroll');
    expect(container.textContent).toContain('Payroll production');
    expect(container.querySelector('.application-history li')?.textContent).toContain('Revision 2');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldDeclareASystemInstanceAgainstTheApplicationRevision', async () => {
    // Arrange
    detailAnswers(2);
    api.reply(`POST ${app}/system-instances`, 200, { system_instance_id: instanceId });
    const container = mount(() => <ApplicationDetailPage applicationId={applicationId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Record a reviewed system"]')).not.toBeNull());
    typeInto(container, 'System name', 'Payroll staging');
    typeInto(container, 'Kind', 'staging tenant');

    // Act
    submit(container, 'Record a reviewed system');
    await vi.waitFor(() => expect(api.bodies.some((b) => b.method === 'POST' && b.path === `${app}/system-instances`)).toBe(true));

    // Assert
    const sent = api.bodies.find((b) => b.method === 'POST' && b.path === `${app}/system-instances`)?.body as Record<string, unknown>;
    expect(sent).toEqual({ expected_application_revision: 2, name: 'Payroll staging', kind: 'staging tenant', access_boundary_reference: null });
  });

  it('ShouldExplainProjectionLagGivenATransientConflict', async () => {
    // Arrange
    api.reply(`GET ${app}`, 409, problem(409, 'Projection is behind.', true));

    // Act
    const container = mount(() => <ApplicationDetailPage applicationId={applicationId} />);
    await vi.waitFor(() => expect(container.querySelector('h1')).not.toBeNull());

    // Assert
    expect(container.textContent).toContain('still being processed');
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Try again')).toBe(true);
  });

  it('ShouldShowNotFoundGivenAnUnknownApplication', async () => {
    // Arrange
    api.reply(`GET ${app}`, 404, problem(404, 'The application was not found.'));

    // Act
    const container = mount(() => <ApplicationDetailPage applicationId={applicationId} />);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Application not found'));

    // Assert
    expect(container.querySelector('a[href="/acme/applications"]')).not.toBeNull();
  });
});

describe('application change and retirement (R1-10d frontend #218)', () => {
  it('ShouldPreviewImpactBeforeSavingARevision', async () => {
    // Arrange
    detailAnswers(2);
    api.reply(`POST ${app}/change-previews`, 200, preview('revise'));
    api.reply(`PUT ${app}`, 204);
    const container = mount(() => <ApplicationDetailPage applicationId={applicationId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Revise application"]')).not.toBeNull());
    typeInto(container, 'Owner', 'Finance');

    // Act
    submit(container, 'Revise application');
    await vi.waitFor(() => expect(container.textContent).toContain('Change impact'));
    expect(api.bodies.some((b) => b.method === 'PUT')).toBe(false);
    expect(container.textContent).toContain('Owner: not set → Finance');
    expect(container.textContent).toContain('Processes customer data.');
    expect(container.textContent).toContain('This preview is not complete');
    [...container.querySelectorAll('button')].find((b) => b.textContent === 'Save revision')!.click();
    await vi.waitFor(() => expect(api.bodies.some((b) => b.method === 'PUT')).toBe(true));

    // Assert
    const sent = api.bodies.find((b) => b.method === 'PUT')?.body as Record<string, unknown>;
    expect(sent.expected_revision).toBe(2);
    expect(sent.owner_reference).toBe('Finance');
    await vi.waitFor(() => expect(api.requested.filter((p) => p === app).length).toBeGreaterThan(1));
  });

  it('ShouldAskToReloadGivenAStaleRevisionConflict', async () => {
    // Arrange
    detailAnswers(2);
    api.reply(`POST ${app}/change-previews`, 409, problem(409, 'The application revision is stale; current revision is 3.'));
    const container = mount(() => <ApplicationDetailPage applicationId={applicationId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Revise application"]')).not.toBeNull());

    // Act
    submit(container, 'Revise application');
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('Someone else changed this application');
  });

  it('ShouldPreviewThenRetireWithReasonAndEffectiveDate', async () => {
    // Arrange
    detailAnswers(2);
    api.reply(`POST ${app}/change-previews`, 200, preview('retire'));
    api.reply(`POST ${app}/retirements`, 204);
    const container = mount(() => <ApplicationDetailPage applicationId={applicationId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Retire application"]')).not.toBeNull());
    typeInto(container, 'Reason for retirement', 'Replaced by Workday');
    typeInto(container, 'Effective date', '2026-10-01');

    // Act
    submit(container, 'Retire application');
    await vi.waitFor(() => expect(container.textContent).toContain('Retirement impact'));
    [...container.querySelectorAll('button')].find((b) => b.textContent === 'Retire application')!.click();
    await vi.waitFor(() => expect(api.bodies.some((b) => b.path === `${app}/retirements`)).toBe(true));

    // Assert
    expect(api.bodies.find((b) => b.path === `${app}/change-previews`)?.body).toEqual({
      expected_application_revision: 2,
      change_kind: 'retire',
    });
    expect(api.bodies.find((b) => b.path === `${app}/retirements`)?.body).toEqual({
      expected_revision: 2,
      effective_at: '2026-10-01T00:00:00Z',
      reason: 'Replaced by Workday',
    });
  });

  it('ShouldShowRetirementAndHideEditsGivenARetiredApplication', async () => {
    // Arrange
    detailAnswers(3);
    api.reply(`GET ${app}`, 200, applicationBody(3, {
      lifecycle: 'retired',
      retirement: { effective_at: '2026-10-01T00:00:00Z', reason: 'Replaced by Workday', merged_into_application_id: null },
    }));

    // Act
    const container = mount(() => <ApplicationDetailPage applicationId={applicationId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Replaced by Workday'));

    // Assert
    expect(container.querySelector('form[aria-label="Revise application"]')).toBeNull();
    expect(container.querySelector('form[aria-label="Retire application"]')).toBeNull();
  });
});

describe('access-review scope (R1-10c frontend #216)', () => {
  it('ShouldShowUnresolvedScopeAndDecideAgainstExpectedCounts', async () => {
    // Arrange
    detailAnswers();
    api.reply(`GET ${instancePath}/access-review-scope`, 200, {
      tenant_id: tenantId, application_id: applicationId, system_instance_id: instanceId,
      as_of: '2026-09-30T00:00:00Z', status: 'unresolved', effective: null, decisions: [],
    });
    api.reply(`POST ${instancePath}/access-review-scope-decisions`, 200, decision);
    const container = mount(() => <ApplicationDetailPage applicationId={applicationId} />);
    await openScope(container);
    expect(container.textContent).toContain('Unresolved: no scope decision applies');
    typeInto(container, 'Rationale', 'Holds payroll data');
    typeInto(container, 'Effective from', '2026-10-01');

    // Act
    submit(container, 'Decide access-review scope');
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(api.bodies.find((b) => b.path === `${instancePath}/access-review-scope-decisions`)?.body).toEqual({
      expected_system_instance_revision: 1,
      expected_decision_count: 0,
      decision: 'included',
      reason: 'Holds payroll data',
      effective_from: '2026-10-01T00:00:00Z',
    });
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldShowCurrentDecisionAndHistoryAsOfADate', async () => {
    // Arrange
    detailAnswers();
    api.reply(`GET ${instancePath}/access-review-scope`, 200, {
      tenant_id: tenantId, application_id: applicationId, system_instance_id: instanceId,
      as_of: '2026-09-30T00:00:00Z', status: 'included', effective: decision, decisions: [decision],
    });
    const container = mount(() => <ApplicationDetailPage applicationId={applicationId} />);
    await openScope(container);

    // Act
    typeInto(container, 'Show decision as of', '2026-09-25');
    await vi.waitFor(() => expect(api.requested.filter((p) => p === `${instancePath}/access-review-scope`)).toHaveLength(2));

    // Assert
    expect(container.textContent).toContain('Included in access reviews');
    expect(container.querySelectorAll('.scope-history li')).toHaveLength(1);
    expect(container.querySelector('.scope-history li')?.textContent).toContain('Casey Lead');
  });

  it('ShouldExplainSeparationOfDutiesGivenTheRegistrarDecides', async () => {
    // Arrange
    detailAnswers();
    api.reply(`GET ${instancePath}/access-review-scope`, 200, {
      tenant_id: tenantId, application_id: applicationId, system_instance_id: instanceId,
      as_of: '2026-09-30T00:00:00Z', status: 'unresolved', effective: null, decisions: [],
    });
    api.reply(
      `POST ${instancePath}/access-review-scope-decisions`,
      403,
      problem(403, 'The member who registered the system instance cannot approve its access-review scope without an active exact-scope waiver.')
    );
    const container = mount(() => <ApplicationDetailPage applicationId={applicationId} />);
    await openScope(container);
    typeInto(container, 'Rationale', 'Holds payroll data');

    // Act
    submit(container, 'Decide access-review scope');
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('Separation of duties');
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('waiver');
  });

  it('ShouldShowForbiddenGivenTheViewerCannotDecideScope', async () => {
    // Arrange
    detailAnswers();
    api.reply(`GET ${instancePath}/access-review-scope`, 403, problem(403, 'Forbidden'));
    const container = mount(() => <ApplicationDetailPage applicationId={applicationId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Payroll production'));

    // Act
    [...container.querySelectorAll('button')].find((b) => b.textContent?.startsWith('Access-review scope for'))!.click();
    await vi.waitFor(() => expect(container.textContent).toContain('You do not have permission to view this access-review scope'));

    // Assert
    expect(container.querySelector('form[aria-label="Decide access-review scope"]')).toBeNull();
  });
});
