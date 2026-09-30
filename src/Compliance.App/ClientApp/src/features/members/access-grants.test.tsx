// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { AccessGrantsCard } from './pages/access-grants-card.js';
import type { MemberAccess } from './members.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const programId = '0190a1b2-0000-7000-8000-0000000000b1';
const base = `/api/v1/tenants/${tenantId}`;
let api: ReturnType<typeof stubApi>;
const mounted: RenderResult[] = [];

function mount(component: () => unknown): HTMLElement {
  const result = render(component as never);
  mounted.push(result);
  return result.container;
}

function access(grants: unknown[]): MemberAccess {
  return {
    tenant_id: tenantId,
    user_id: 'user-aa',
    member_id: 'member-aa',
    paths: [],
    grant_paths: grants as MemberAccess['grant_paths'],
    effective_permissions: [],
  };
}

function grantPath(id: string, scope: { kind: string; id: string }, revokedAt: string | null = null) {
  return {
    grant: {
      tenant_id: tenantId,
      grant_id: id,
      terms: {
        principal: { kind: 'member', id: 'member-aa' },
        role_id: 'role-lead',
        scope,
        source: { kind: 'manual', id },
        granted_by: { kind: 'member', id: 'admin', display: 'Alex Admin' },
        effective_from: '2026-09-01T00:00:00Z',
        effective_until: null,
      },
      revoked_by: revokedAt ? { kind: 'member', id: 'admin', display: 'Alex Admin' } : null,
      revoked_at: revokedAt,
    },
    role_name: 'Compliance Lead',
    permissions: [],
    is_effective: revokedAt === null,
  };
}

function catalog() {
  api.reply(`${base}/roles`, 200, {
    items: [{ role_id: 'role-lead', name: 'Compliance Lead' }],
    next_cursor: null,
  });
  api.reply(`${base}/programs`, 200, {
    items: [{ tenant_id: tenantId, program_id: programId, name: 'SOC 2 2026' }],
    next_cursor: null,
  });
}

function choose(container: HTMLElement, index: number, value: string) {
  const select = container.querySelectorAll('select')[index] as HTMLSelectElement;
  select.value = value;
  select.dispatchEvent(new Event('change', { bubbles: true }));
}

beforeEach(async () => {
  clearActiveTenant();
  api = stubApi();
  api.reply('/api/v1/tenant-slugs/acme/mine', 200, { tenant_id: tenantId, current_slug: 'acme', redirect: false });
  api.reply('/api/v1/tenants/mine', 200, {
    items: [{ tenant_id: tenantId, name: 'Acme Corp', slug: 'acme' }],
    next_cursor: null,
  });
  await resolveTenantRoute('acme', { pathname: '/acme/members/user-aa', search: '', hash: '' });
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('scoped access grants (R1-04b frontend #187)', () => {
  it('ShouldGrantARoleOnAProgramScopeForTheMember', async () => {
    // Arrange
    catalog();
    const onChanged = vi.fn();
    const container = mount(() => <AccessGrantsCard access={access([])} onChanged={onChanged} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Program: SOC 2 2026'));
    choose(container, 0, 'role-lead');
    choose(container, 1, programId);
    api.reply(`POST ${base}/access-grants/*`, 204);

    // Act
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    const sent = api.bodies.find((b) => b.method === 'POST' && b.path.startsWith(`${base}/access-grants/`));
    expect(sent).toBeDefined();
    const proposal = (sent!.body as { proposal: Record<string, unknown> }).proposal;
    expect(proposal.principal).toEqual({ kind: 'member', id: 'member-aa' });
    expect(proposal.role_id).toBe('role-lead');
    expect(proposal.scope).toEqual({ kind: 'program', id: programId });
    expect(proposal.effective_until).toBeNull();
    expect(onChanged).toHaveBeenCalledOnce();
    expect(container.querySelector('[role="status"]')?.textContent).toBe('Granted Compliance Lead on Program: SOC 2 2026.');
  });

  it('ShouldRevokeOnlyActiveGrantsAndStateTheAccessLoss', async () => {
    // Arrange
    catalog();
    api.reply(`DELETE ${base}/access-grants/grant-1`, 204);
    const container = mount(() => (
      <AccessGrantsCard
        access={access([
          grantPath('grant-1', { kind: 'organization', id: tenantId }),
          grantPath('grant-2', { kind: 'program', id: programId }, '2026-09-20T00:00:00Z'),
        ])}
        onChanged={() => {}}
      />
    ));
    await vi.waitFor(() => expect(container.textContent).toContain('Program: SOC 2 2026'));

    // Act
    const revokes = [...container.querySelectorAll('.grant-row button')] as HTMLButtonElement[];
    revokes[0]!.click();
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(revokes).toHaveLength(1);
    expect(api.bodies.some((b) => b.method === 'DELETE' && b.path === `${base}/access-grants/grant-1`)).toBe(true);
    expect([...container.querySelectorAll('.grant-status')].map((s) => s.textContent)).toEqual(['active', 'revoked']);
    expect(container.querySelector('[role="status"]')?.textContent).toContain('lost the access it provided');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldPreserveTheServerReasonGivenAGrantConflict', async () => {
    // Arrange
    catalog();
    const container = mount(() => <AccessGrantsCard access={access([])} onChanged={() => {}} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Program: SOC 2 2026'));
    choose(container, 0, 'role-lead');
    choose(container, 1, tenantId);
    api.reply(`POST ${base}/access-grants/*`, 409, {
      type: 'about:blank', title: 'Conflict', status: 409, instance: '/',
      detail: 'The member already holds this role on that scope.',
    });

    // Act
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toBe('The member already holds this role on that scope.');
  });

  it('ShouldOfferRetryGivenTheRoleOrScopeCatalogFailedToLoad', async () => {
    // Arrange
    api.reply(`${base}/roles`, 503);

    // Act
    const container = mount(() => <AccessGrantsCard access={access([])} onChanged={() => {}} />);
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('form')).toBeNull();
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Try again')).toBe(true);
  });
});
