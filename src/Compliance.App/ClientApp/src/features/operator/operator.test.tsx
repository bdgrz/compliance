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
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(api.bodies.some((b) => b.method === 'POST' && b.path === '/api/v1/tenants/t1/suspensions')).toBe(true);
    expect(container.querySelector('[role="status"]')?.textContent).toContain('cannot sign in to it');
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
