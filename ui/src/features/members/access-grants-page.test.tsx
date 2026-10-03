// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { AccessGrantsPage } from './pages/access-grants.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const programId = '0190a1b2-0000-7000-8000-0000000000b1';
const teamId = '0190a1b2-0000-7000-8000-0000000000c1';
const base = `/api/v1/tenants/${tenantId}`;
let api: ReturnType<typeof stubApi>;
const mounted: RenderResult[] = [];

function mount(component: () => unknown): HTMLElement {
  const result = render(component as never);
  mounted.push(result);
  return result.container;
}

function grant(
  id: string,
  extra: { principal?: { kind: string; id: string }; scope?: { kind: string; id: string }; until?: string | null; revoked?: boolean } = {}
) {
  return {
    tenant_id: tenantId,
    grant_id: id,
    terms: {
      principal: extra.principal ?? { kind: 'member', id: 'abcdef12-0000-0000-0000-000000000000' },
      role_id: 'role-lead',
      scope: extra.scope ?? { kind: 'organization', id: tenantId },
      source: { kind: 'manual', id },
      granted_by: { kind: 'member', id: 'admin', display: 'Alex Admin' },
      effective_from: '2026-09-01T00:00:00Z',
      effective_until: extra.until ?? null,
    },
    revoked_by: extra.revoked ? { kind: 'member', id: 'admin', display: 'Riley Admin' } : null,
    revoked_at: extra.revoked ? '2026-09-10T00:00:00Z' : null,
  };
}

function catalog() {
  api.reply(`${base}/roles`, 200, { items: [{ role_id: 'role-lead', name: 'Compliance Lead' }], next_cursor: null });
  api.reply(`${base}/programs`, 200, { items: [{ tenant_id: tenantId, program_id: programId, name: 'SOC 2 2026' }], next_cursor: null });
  api.reply(`${base}/teams`, 200, { items: [{ team_id: teamId, name: 'Security' }], next_cursor: null });
}

function grants(...items: unknown[]) {
  api.reply(`GET ${base}/access-grants`, 200, { tenant_id: tenantId, revision: 3, grants: items });
}

beforeEach(async () => {
  clearActiveTenant();
  api = stubApi();
  api.reply('/api/v1/tenant-slugs/acme/mine', 200, { tenant_id: tenantId, current_slug: 'acme', redirect: false });
  api.reply('/api/v1/tenants/mine', 200, { items: [{ tenant_id: tenantId, name: 'Acme Corp', slug: 'acme' }], next_cursor: null });
  await resolveTenantRoute('acme', { pathname: '/acme/access-grants', search: '', hash: '' });
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('organization access grants (R1-04b frontend #187)', () => {
  it('ShouldListCurrentGrantsWithScopeSourceGrantorAndInterval', async () => {
    // Arrange
    catalog();
    grants(
      grant('g1'),
      grant('g2', { principal: { kind: 'team', id: teamId }, scope: { kind: 'program', id: programId }, until: '2099-01-01T00:00:00Z' }),
      grant('g3', { revoked: true })
    );

    // Act
    const container = mount(AccessGrantsPage);
    await vi.waitFor(() => expect(container.textContent).toContain('Program: SOC 2 2026'));

    // Assert
    const rows = [...container.querySelectorAll('tbody tr')].map((row) => row.textContent);
    expect(rows).toHaveLength(2);
    expect(rows[0]).toContain('Member abcdef12');
    expect(rows[0]).toContain('Whole organization');
    expect(rows[0]).toContain('Granted directly');
    expect(rows[0]).toContain('Alex Admin');
    expect(rows[1]).toContain('Team Security');
    expect(rows[1]).toContain('active');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldShowRevokedAndExpiredGrantsOnRequest', async () => {
    // Arrange
    catalog();
    grants(grant('g1', { revoked: true }), grant('g2', { until: '2026-09-02T00:00:00Z' }));
    const container = mount(AccessGrantsPage);
    await vi.waitFor(() => expect(container.textContent).toContain('No active or scheduled access grants'));
    const select = container.querySelector('select') as HTMLSelectElement;

    // Act
    select.value = 'all';
    select.dispatchEvent(new Event('change', { bubbles: true }));
    await vi.waitFor(() => expect(container.querySelectorAll('tbody tr')).toHaveLength(2));

    // Assert
    const rows = [...container.querySelectorAll('tbody tr')].map((row) => row.textContent);
    expect(rows[0]).toContain('revoked by Riley Admin');
    expect(rows[1]).toContain('expired');
    expect(container.querySelector('tbody button')).toBeNull();
  });

  it('ShouldRevokeThroughTheServerAndRefresh', async () => {
    // Arrange
    catalog();
    grants(grant('g1'));
    api.reply(`DELETE ${base}/access-grants/g1`, 204);
    const container = mount(AccessGrantsPage);
    await vi.waitFor(() => expect(container.querySelector('tbody button')).not.toBeNull());

    // Act
    (container.querySelector('tbody button') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(api.bodies.some((b) => b.method === 'DELETE' && b.path === `${base}/access-grants/g1`)).toBe(true);
    expect(api.requested.filter((p) => p === `${base}/access-grants`).length).toBeGreaterThanOrEqual(2);
  });

  it('ShouldPreserveTheServerReasonGivenARevocationConflict', async () => {
    // Arrange
    catalog();
    grants(grant('g1'));
    api.reply(`DELETE ${base}/access-grants/g1`, 409, {
      type: 'about:blank', title: 'Conflict', status: 409, detail: 'The grant is already revoked.', instance: '/',
    });
    const container = mount(AccessGrantsPage);
    await vi.waitFor(() => expect(container.querySelector('tbody button')).not.toBeNull());

    // Act
    (container.querySelector('tbody button') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toBe('The grant is already revoked.');
  });

  it('ShouldShowForbiddenWithoutGrantContent', async () => {
    // Arrange
    catalog();
    api.reply(`GET ${base}/access-grants`, 403, { type: 'about:blank', title: 'Forbidden', status: 403, detail: 'No.', instance: '/' });

    // Act
    const container = mount(AccessGrantsPage);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Access grants are not available to you'));

    // Assert
    expect(container.querySelector('table')).toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldOfferRetryGivenAFailure', async () => {
    // Arrange
    catalog();
    api.reply(`GET ${base}/access-grants`, 503);

    // Act
    const container = mount(AccessGrantsPage);
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Try again')).toBe(true);
  });
});
