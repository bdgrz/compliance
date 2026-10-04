// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { TenantInventoryPage } from './pages/tenant-inventory.js';

const inventory = '/api/v1/platform/tenants';
let api: ReturnType<typeof stubApi>;
const mounted: RenderResult[] = [];

function mount(component: () => unknown): HTMLElement {
  const result = render(component as never);
  mounted.push(result);
  return result.container;
}

function tenant(id: string, name: string, overrides: Record<string, unknown> = {}) {
  return { tenant_id: id, name, slug: name.toLowerCase().replace(/\W+/g, '-'), status: 'active', ...overrides };
}

beforeEach(() => {
  api = stubApi();
  api.reply('/api/v1/platform/operators', 200, { user_ids: [] });
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('operator organization inventory (R1-15a frontend #239)', () => {
  it('ShouldShowMetadataAndProvisioningWithoutImplyingRecordAccess', async () => {
    // Arrange
    api.reply(inventory, 200, {
      items: [
        tenant('t1', 'Acme Corp', { legal_name: 'Acme Corporation Inc.' }),
        tenant('t2', 'Beta LLC', { requires_invitation: true }),
        tenant('t3', 'Gamma Co', { status: 'suspended', requires_activation: true }),
      ],
      next_cursor: null,
    });

    // Act
    const container = mount(TenantInventoryPage);
    await vi.waitFor(() => expect(container.querySelectorAll('tbody tr')).toHaveLength(3));

    // Assert
    const rows = [...container.querySelectorAll('tbody tr')].map((r) => r.textContent);
    expect(rows[0]).toContain('Acme Corporation Inc.');
    expect(rows[1]).toContain('First administrator not invited');
    expect(rows[2]).toContain('Suspended');
    expect(rows[2]).toContain('Reactivate');
    expect(container.textContent).toContain('does not open any organization');
    expect(container.querySelector('a[href="/acme-corp"]')).toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldPageThroughTheInventoryWithTheServerCursor', async () => {
    // Arrange
    api.reply(inventory, 200, { items: [tenant('t1', 'Acme Corp')], next_cursor: 'cursor-2' });
    const container = mount(TenantInventoryPage);
    await vi.waitFor(() => expect(container.textContent).toContain('Acme Corp'));
    api.reply(inventory, 200, { items: [tenant('t2', 'Beta LLC')], next_cursor: null });

    // Act
    [...container.querySelectorAll('button')].find((b) => b.textContent === 'Next page')!.click();
    await vi.waitFor(() => expect(container.textContent).toContain('Beta LLC'));

    // Assert
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Previous page')).toBe(true);
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Next page')).toBe(false);
  });

  it('ShouldSuspendAnOrganizationAndStateTheConsequence', async () => {
    // Arrange
    api.reply(inventory, 200, { items: [tenant('t1', 'Acme Corp')], next_cursor: null });
    api.reply('POST /api/v1/tenants/t1/suspensions', 204);
    const container = mount(TenantInventoryPage);
    await vi.waitFor(() => expect(container.querySelector('button[aria-label="Suspend Acme Corp"]')).not.toBeNull());

    // Act
    (container.querySelector('button[aria-label="Suspend Acme Corp"]') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());
    expect(await accessibilityViolations(container)).toEqual([]);
    expect(api.bodies.some((b) => b.path === '/api/v1/tenants/t1/suspensions')).toBe(false);
    const reason = container.querySelector('textarea[name="reason"]') as HTMLTextAreaElement;
    reason.value = 'Client requested an access pause.';
    reason.dispatchEvent(new Event('input', { bubbles: true }));
    (container.querySelector('form') as HTMLFormElement).dispatchEvent(
      new Event('submit', { bubbles: true, cancelable: true })
    );
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    const sent = api.bodies.find((b) => b.method === 'POST' && b.path === '/api/v1/tenants/t1/suspensions');
    expect(sent?.body).toEqual({ reason: 'Client requested an access pause.' });
    expect(container.querySelector('[role="status"]')?.textContent).toContain('cannot sign in to it');
  });

  it('ShouldRequireALifecycleReasonBeforeCallingTheServer', async () => {
    // Arrange
    api.reply(inventory, 200, { items: [tenant('t1', 'Acme Corp')], next_cursor: null });
    const container = mount(TenantInventoryPage);
    await vi.waitFor(() => expect(container.querySelector('button[aria-label="Suspend Acme Corp"]')).not.toBeNull());

    // Act
    (container.querySelector('button[aria-label="Suspend Acme Corp"]') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());
    (container.querySelector('form') as HTMLFormElement).dispatchEvent(
      new Event('submit', { bubbles: true, cancelable: true })
    );
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toBe('Enter a reason with at most 500 characters.');
    expect(api.bodies.some((b) => b.path === '/api/v1/tenants/t1/suspensions')).toBe(false);
  });

  it('ShouldReactivateAnOrganizationWithAnAttributedReason', async () => {
    // Arrange
    api.reply(inventory, 200, { items: [tenant('t1', 'Acme Corp', { status: 'suspended' })], next_cursor: null });
    api.reply('DELETE /api/v1/tenants/t1/suspensions', 204);
    const container = mount(TenantInventoryPage);
    await vi.waitFor(() => expect(container.querySelector('button[aria-label="Reactivate Acme Corp"]')).not.toBeNull());

    // Act
    (container.querySelector('button[aria-label="Reactivate Acme Corp"]') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());
    const reason = container.querySelector('textarea[name="reason"]') as HTMLTextAreaElement;
    reason.value = 'The access review is complete.';
    reason.dispatchEvent(new Event('input', { bubbles: true }));
    (container.querySelector('form') as HTMLFormElement).dispatchEvent(
      new Event('submit', { bubbles: true, cancelable: true })
    );
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    const sent = api.bodies.find((b) => b.method === 'DELETE' && b.path === '/api/v1/tenants/t1/suspensions');
    expect(sent?.body).toEqual({ reason: 'The access review is complete.' });
    expect(container.querySelector('[role="status"]')?.textContent).toContain('is reactivated');
  });

  it('ShouldShowOperatorOnlyGivenAForbiddenViewer', async () => {
    // Arrange
    api.reply(inventory, 403, { type: 'about:blank', title: 'Forbidden', status: 403, detail: 'x', instance: '/' });

    // Act
    const container = mount(TenantInventoryPage);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Operator access required'));

    // Assert
    expect(container.querySelector('table')).toBeNull();
  });

  it('ShouldShowTheServerReasonGivenASuspensionConflict', async () => {
    // Arrange
    api.reply(inventory, 200, { items: [tenant('t1', 'Acme Corp')], next_cursor: null });
    api.reply('POST /api/v1/tenants/t1/suspensions', 409, {
      type: 'about:blank', title: 'Conflict', status: 409, instance: '/',
      detail: 'The organization is still being provisioned.',
    });
    const container = mount(TenantInventoryPage);
    await vi.waitFor(() => expect(container.querySelector('button[aria-label="Suspend Acme Corp"]')).not.toBeNull());

    // Act
    (container.querySelector('button[aria-label="Suspend Acme Corp"]') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());
    const reason = container.querySelector('textarea[name="reason"]') as HTMLTextAreaElement;
    reason.value = 'Temporary incident response.';
    reason.dispatchEvent(new Event('input', { bubbles: true }));
    (container.querySelector('form') as HTMLFormElement).dispatchEvent(
      new Event('submit', { bubbles: true, cancelable: true })
    );
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toBe('The organization is still being provisioned.');
  });

  it('ShouldOfferRetryAndEmptyStates', async () => {
    // Arrange
    api.reply(inventory, 503);
    const container = mount(TenantInventoryPage);
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());
    api.reply(inventory, 200, { items: [], next_cursor: null });

    // Act
    [...container.querySelectorAll('button')].find((b) => b.textContent === 'Try again')!.click();
    await vi.waitFor(() => expect(container.textContent).toContain('No organizations have been provisioned yet.'));

    // Assert
    expect(container.querySelector('[role="alert"]')).toBeNull();
  });
});

describe('operator slug change and firm-staff membership (R1-15 frontend #539)', () => {
  function problem(status: number, detail: string) {
    return { type: 'about:blank', title: 'Problem', status, detail, instance: '/' };
  }

  async function openPanel(container: HTMLElement, label: string) {
    await vi.waitFor(() => expect(container.querySelector(`button[aria-label="${label}"]`)).not.toBeNull());
    (container.querySelector(`button[aria-label="${label}"]`) as HTMLButtonElement).click();
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());
  }

  function type(container: HTMLElement, selector: string, value: string) {
    const input = container.querySelector(selector) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input', { bubbles: true }));
  }

  function submit(container: HTMLElement) {
    (container.querySelector('form') as HTMLFormElement).dispatchEvent(
      new Event('submit', { bubbles: true, cancelable: true })
    );
  }

  it('ShouldRequestASlugChangeAndExplainTheRedirectGivenAValidSlug', async () => {
    // Arrange
    api.reply(inventory, 200, { items: [tenant('t1', 'Acme Corp')], next_cursor: null });
    api.reply('POST /api/v1/tenants/t1/slug-changes', 204);
    const container = mount(TenantInventoryPage);
    await openPanel(container, 'Change address of Acme Corp');
    expect(await accessibilityViolations(container)).toEqual([]);

    // Act
    type(container, 'input[name="slug"]', 'acme-group');
    submit(container);
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    const sent = api.bodies.find((b) => b.path === '/api/v1/tenants/t1/slug-changes');
    expect(sent?.body).toEqual({ slug: 'acme-group' });
    expect(container.querySelector('[role="status"]')?.textContent).toContain('/acme-corp');
    expect(container.querySelector('[role="status"]')?.textContent).toContain('/acme-group');
    expect(container.querySelector('[role="status"]')?.textContent).toContain('redirect');
  });

  it('ShouldRejectAnInvalidSlugWithoutCallingTheServer', async () => {
    // Arrange
    api.reply(inventory, 200, { items: [tenant('t1', 'Acme Corp')], next_cursor: null });
    const container = mount(TenantInventoryPage);
    await openPanel(container, 'Change address of Acme Corp');

    // Act
    type(container, 'input[name="slug"]', 'Bad Slug!');
    submit(container);
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('lowercase');
    expect(api.bodies.some((b) => b.path.endsWith('/slug-changes'))).toBe(false);
  });

  it('ShouldShowTheServerReasonGivenATakenSlug', async () => {
    // Arrange
    api.reply(inventory, 200, { items: [tenant('t1', 'Acme Corp')], next_cursor: null });
    api.reply('POST /api/v1/tenants/t1/slug-changes', 409, problem(409, 'The tenant slug is already reserved or retired.'));
    const container = mount(TenantInventoryPage);
    await openPanel(container, 'Change address of Acme Corp');

    // Act
    type(container, 'input[name="slug"]', 'taken-slug');
    submit(container);
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toBe('The tenant slug is already reserved or retired.');
  });

  it('ShouldExplainFirmStaffHaveNoBusinessAccessAndInviteWithFirmAffiliation', async () => {
    // Arrange
    api.reply(inventory, 200, { items: [tenant('t1', 'Acme Corp')], next_cursor: null });
    api.reply('POST /api/v1/tenants/t1/invitations', 204);
    const container = mount(TenantInventoryPage);
    await openPanel(container, 'Add firm staff to Acme Corp');
    expect(container.textContent).toContain('no access to business records');
    expect(container.textContent).toContain('accepted engagement assignment');
    expect(await accessibilityViolations(container)).toEqual([]);

    // Act
    type(container, 'input[type="email"]', 'advisor@firm.test');
    submit(container);
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    const sent = api.bodies.find((b) => b.path === '/api/v1/tenants/t1/invitations');
    expect(sent?.body).toEqual({ email_address: 'advisor@firm.test', affiliation: 'firm_staff', administrator: false });
    expect(container.querySelector('[role="status"]')?.textContent).toContain('no business-record access');
  });

  it('ShouldShowTheServerReasonGivenAFirmStaffInvitationFailure', async () => {
    // Arrange
    api.reply(inventory, 200, { items: [tenant('t1', 'Acme Corp')], next_cursor: null });
    api.reply('POST /api/v1/tenants/t1/invitations', 400, problem(400, 'Enter a valid email address.'));
    const container = mount(TenantInventoryPage);
    await openPanel(container, 'Add firm staff to Acme Corp');

    // Act
    type(container, 'input[type="email"]', 'nope');
    submit(container);
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toBe('Enter a valid email address.');
  });
});
