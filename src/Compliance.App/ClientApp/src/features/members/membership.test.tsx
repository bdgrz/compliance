// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { MembershipPanel } from './pages/membership-panel.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const userId = '0190a1b2-0000-7000-8000-0000000000aa';
const member = `/api/v1/tenants/${tenantId}/members/${userId}`;
let api: ReturnType<typeof stubApi>;
const mounted: RenderResult[] = [];

function mount(component: () => unknown): HTMLElement {
  const result = render(component as never);
  mounted.push(result);
  return result.container;
}

function membership(suspended: boolean) {
  api.reply(`GET ${member}`, 200, {
    user_id: userId,
    tenant_id: tenantId,
    affiliation: 'client_personnel',
    is_suspended: suspended,
    suspended_at: suspended ? '2026-09-29T12:00:00Z' : null,
    suspended_by_display: suspended ? 'Alex Admin' : null,
    suspension_reason: suspended ? 'Left the company' : null,
  });
}

function responsibility(overrides: Record<string, unknown>) {
  return {
    tenant_id: tenantId,
    assignment_id: 'a-1',
    member_id: 'm',
    type: 'control_owner',
    scope: { record_type: 'control', record_id: '0190a1b2-0000-7000-8000-0000000000c1', version_id: 'v', revision: 3 },
    assigned_at: '2026-09-01T00:00:00Z',
    assigned_by_member_id: 'admin',
    effective_from: '2026-09-01T00:00:00Z',
    effective_until: null,
    revoked_at: null,
    revoked_by_member_id: '',
    separation_of_duties_waiver_ids: [],
    ...overrides,
  };
}

beforeEach(async () => {
  clearActiveTenant();
  api = stubApi();
  api.reply('/api/v1/tenant-slugs/acme/mine', 200, { tenant_id: tenantId, current_slug: 'acme', redirect: false });
  api.reply('/api/v1/tenants/mine', 200, {
    items: [{ tenant_id: tenantId, name: 'Acme Corp', slug: 'acme' }],
    next_cursor: null,
  });
  await resolveTenantRoute('acme', { pathname: '/acme/members', search: '', hash: '' });
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('member suspension (R1-04d frontend #191)', () => {
  it('ShouldExplainTheAccessLossAndSendTheReasonGivenASuspension', async () => {
    // Arrange
    membership(false);
    api.reply(`${member}/responsibilities`, 200, []);
    api.reply(`POST ${member}/suspensions`, 204);
    const container = mount(() => <MembershipPanel userId={userId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('ends all of this member'));
    const reason = container.querySelector('textarea') as HTMLTextAreaElement;
    reason.value = 'Left the company';
    reason.dispatchEvent(new Event('input', { bubbles: true }));

    // Act
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    const sent = api.bodies.find((b) => b.method === 'POST' && b.path === `${member}/suspensions`);
    expect(sent?.body).toEqual({ reason: 'Left the company' });
    expect(container.querySelector('[role="status"]')?.textContent).toContain('access has ended');
  });

  it('ShouldListOpenWorkNeedingReassignmentGivenASuspendedMember', async () => {
    // Arrange
    membership(true);
    api.reply(`${member}/responsibilities`, 200, [
      responsibility({}),
      responsibility({ assignment_id: 'a-2', revoked_at: '2026-09-10T00:00:00Z' }),
      responsibility({ assignment_id: 'a-3', type: 'assigned_reviewer', effective_until: '2026-09-02T00:00:00Z' }),
    ]);

    // Act
    const container = mount(() => <MembershipPanel userId={userId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Open work needing reassignment'));

    // Assert
    expect(container.textContent).toContain('Suspended by Alex Admin');
    expect(container.textContent).toContain('Left the company');
    const items = [...container.querySelectorAll('.plain-list li')].map((li) => li.textContent);
    expect(items).toHaveLength(1);
    expect(items[0]).toContain('Control owner');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldReinstateGivenASuspendedMember', async () => {
    // Arrange
    membership(true);
    api.reply(`${member}/responsibilities`, 200, []);
    api.reply(`DELETE ${member}/suspensions`, 204);
    const container = mount(() => <MembershipPanel userId={userId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Reinstate member'));

    // Act
    [...container.querySelectorAll('button')].find((b) => b.textContent === 'Reinstate member')!.click();
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(api.bodies.some((b) => b.method === 'DELETE' && b.path === `${member}/suspensions`)).toBe(true);
  });

  it('ShouldShowTheServerReasonGivenASuspensionConflict', async () => {
    // Arrange
    membership(false);
    api.reply(`${member}/responsibilities`, 200, []);
    api.reply(`POST ${member}/suspensions`, 409, {
      type: 'about:blank', title: 'Conflict', status: 409, instance: '/',
      detail: 'The last active tenant manager cannot be suspended.',
    });
    const container = mount(() => <MembershipPanel userId={userId} />);
    await vi.waitFor(() => expect(container.querySelector('textarea')).not.toBeNull());
    const reason = container.querySelector('textarea') as HTMLTextAreaElement;
    reason.value = 'Test';
    reason.dispatchEvent(new Event('input', { bubbles: true }));

    // Act
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toBe(
      'The last active tenant manager cannot be suspended.'
    );
  });

  it('ShouldStateNoBusinessRecordAccessGivenAFirmStaffMembership', async () => {
    // Arrange
    api.reply(`GET ${member}`, 200, {
      user_id: userId,
      tenant_id: tenantId,
      affiliation: 'firm_staff',
      is_suspended: false,
    });
    api.reply(`${member}/responsibilities`, 200, []);

    // Act
    const container = mount(() => <MembershipPanel userId={userId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Firm staff'));

    // Assert
    expect(container.textContent).toContain('no access to business records until an accepted engagement assignment');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldShowForbiddenWithoutRetryGivenTheViewerCannotManageMembers', async () => {
    // Arrange
    api.reply(`GET ${member}`, 403, { type: 'about:blank', title: 'Forbidden', status: 403, detail: 'x', instance: '/' });

    // Act
    const container = mount(() => <MembershipPanel userId={userId} />);
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('do not have permission');
    expect(container.querySelector('form')).toBeNull();
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Try again')).toBe(false);
  });
});
