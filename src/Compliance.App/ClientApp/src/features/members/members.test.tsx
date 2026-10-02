// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { MemberAccessPage } from './pages/member-access.js';
import { MembersPage } from './pages/members-list.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const userId = '0190a1b2-0000-7000-8000-0000000000aa';
const base = `/api/v1/tenants/${tenantId}`;
let api: ReturnType<typeof stubApi>;
const mounted: RenderResult[] = [];

function mount(component: () => unknown): HTMLElement {
  const result = render(component as never);
  mounted.push(result);
  return result.container;
}

function listsAnswer() {
  api.reply(`${base}/members`, 200, {
    items: [{ user_id: userId, tenant_id: tenantId, affiliation: 'client_personnel', is_suspended: false }],
    next_cursor: null,
  });
  api.reply(`GET ${base}/member-invitations`, 200, {
    items: [
      {
        tenant_id: tenantId,
        email_address: 'pat@example.test',
        affiliation: 'client_personnel',
        administrator: false,
        built_in_role: 'compliance_management',
        status: 'pending',
        expires_at: '2026-10-07T00:00:00Z',
        invited_by: 'member-1',
        accepted_user_id: null,
      },
      {
        tenant_id: tenantId,
        email_address: 'done@example.test',
        affiliation: 'client_personnel',
        administrator: false,
        built_in_role: 'compliance_participation',
        status: 'accepted',
        expires_at: '2026-10-07T00:00:00Z',
        invited_by: 'member-1',
        accepted_user_id: userId,
      },
    ],
    next_cursor: null,
  });
}

function problem(status: number, detail: string) {
  return { type: 'about:blank', title: 'Problem', status, detail, instance: '/' };
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

describe('member invitation (R1-04a frontend #184)', () => {
  it('ShouldExplainEachBuiltInRoleGivenTheInviteForm', async () => {
    // Arrange
    listsAnswer();

    // Act
    const container = mount(MembersPage);
    await vi.waitFor(() => expect(container.textContent).toContain('pat@example.test'));

    // Assert
    const options = [...container.querySelectorAll('.role-option')].map((o) => o.textContent);
    expect(options).toHaveLength(3);
    expect(options.join(' ')).toContain('Compliance Lead');
    expect(options.join(' ')).toContain('Manages members, teams, access grants');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldListOnlyPendingInvitationsAndActiveMembers', async () => {
    // Arrange
    listsAnswer();

    // Act
    const container = mount(MembersPage);
    await vi.waitFor(() => expect(container.textContent).toContain('pat@example.test'));

    // Assert
    expect(container.textContent).not.toContain('done@example.test');
    expect(container.textContent).toContain('Compliance Lead');
    expect(container.querySelector(`a[href="/acme/members/${userId}"]`)).not.toBeNull();
  });

  it('ShouldSendTheInvitationWithTheChosenRoleAndRefresh', async () => {
    // Arrange
    listsAnswer();
    api.reply(`POST ${base}/member-invitations`, 204);
    const container = mount(MembersPage);
    await vi.waitFor(() => expect(container.textContent).toContain('pat@example.test'));
    const email = container.querySelector('input[type="email"]') as HTMLInputElement;
    email.value = 'new@example.test';
    email.dispatchEvent(new Event('input', { bubbles: true }));
    const lead = container.querySelector('input[value="compliance_management"]') as HTMLInputElement;
    lead.click();
    lead.dispatchEvent(new Event('change', { bubbles: true }));

    // Act
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    const sent = api.bodies.find((b) => b.method === 'POST' && b.path === `${base}/member-invitations`);
    expect(sent?.body).toEqual({ email_address: 'new@example.test', built_in_role: 'compliance_management' });
    expect(container.querySelector('[role="status"]')?.textContent).toContain('as Compliance Lead');
    await vi.waitFor(() =>
      expect(api.requested.filter((p) => p === `${base}/members`).length).toBeGreaterThan(1)
    );
  });

  it('ShouldShowTheServerReasonGivenAnInvitationConflict', async () => {
    // Arrange
    listsAnswer();
    api.reply(`POST ${base}/member-invitations`, 409, problem(409, 'That email address is already a member.'));
    const container = mount(MembersPage);
    await vi.waitFor(() => expect(container.textContent).toContain('pat@example.test'));
    const email = container.querySelector('input[type="email"]') as HTMLInputElement;
    email.value = 'pat@example.test';
    email.dispatchEvent(new Event('input', { bubbles: true }));

    // Act
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toBe('That email address is already a member.');
  });

  it('ShouldStateFirmStaffHaveNoBusinessAccessUntilAnAcceptedEngagementGivenAFirmStaffMember', async () => {
    // Arrange
    listsAnswer();
    api.reply(`${base}/members`, 200, {
      items: [
        { user_id: userId, tenant_id: tenantId, affiliation: 'client_personnel', is_suspended: false },
        { user_id: 'firm-1', tenant_id: tenantId, affiliation: 'firm_staff', is_suspended: false },
      ],
      next_cursor: null,
    });

    // Act
    const container = mount(MembersPage);
    await vi.waitFor(() => expect(container.textContent).toContain('Firm staff'));

    // Assert
    const rows = [...container.querySelectorAll('.plain-list li')].filter((li) => li.textContent?.includes('Firm staff'));
    expect(rows).toHaveLength(1);
    expect(rows[0]!.textContent).toContain('No business-record access until an accepted engagement assignment');
    expect(container.textContent).toContain('Engagement assignments are accepted separately');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldShowForbiddenGivenTheViewerIsNotAnAdministrator', async () => {
    // Arrange
    api.reply(`${base}/members`, 403, problem(403, 'Forbidden'));
    api.reply(`GET ${base}/member-invitations`, 403, problem(403, 'Forbidden'));

    // Act
    const container = mount(MembersPage);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Members are not available to you'));

    // Assert
    expect(container.querySelector('form')).toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldOfferRetryGivenTheMemberListFailedToLoad', async () => {
    // Arrange
    api.reply(`${base}/members`, 503, problem(503, 'Try again shortly.'));
    api.reply(`GET ${base}/member-invitations`, 200, { items: [], next_cursor: null });

    // Act
    const container = mount(MembersPage);
    await vi.waitFor(() => expect(container.textContent).toContain('Unable to load the members.'));

    // Assert
    const retry = [...container.querySelectorAll('button')].find((b) => b.textContent === 'Try again');
    expect(retry).toBeDefined();
    expect(container.textContent).toContain('No pending invitations.');
  });
});

describe('member access explanation (R1-04a frontend #184)', () => {
  const grant = (overrides: Record<string, unknown>) => ({
    grant: {
      tenant_id: tenantId,
      grant_id: 'grant-1',
      terms: {
        principal: { kind: 'member', id: userId },
        role_id: 'role-1',
        scope: { kind: 'organization', id: tenantId },
        source: { kind: 'manual', id: 'm' },
        granted_by: { kind: 'member', id: 'admin', display: 'Alex Admin' },
        effective_from: '2026-09-01T00:00:00Z',
        effective_until: null,
      },
      revoked_by: null,
      revoked_at: null,
      ...overrides,
    },
    role_name: 'Compliance Lead',
    permissions: ['controls.manage'],
    is_effective: true,
  });

  it('ShouldExplainPermissionsThroughTeamsAndGrants', async () => {
    // Arrange
    api.reply(`${base}/members/${userId}/access`, 200, {
      tenant_id: tenantId,
      user_id: userId,
      member_id: 'member-aa',
      paths: [{ team_id: 't', team_name: 'Standard users', role_id: 'r', role_name: 'Contributor', permissions: ['evidence.submit'] }],
      grant_paths: [grant({}), { ...grant({ revoked_at: '2026-09-20T00:00:00Z' }), role_name: 'Org Admin' }],
      effective_permissions: ['controls.manage', 'evidence.submit'],
    });

    // Act
    const container = mount(() => <MemberAccessPage userId={userId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Effective permissions'));

    // Assert
    expect(container.textContent).toContain('controls.manage');
    expect(container.textContent).toContain('Standard users');
    expect(container.textContent).toContain('granted by Alex Admin');
    expect([...container.querySelectorAll('.grant-status')].map((s) => s.textContent)).toEqual(['active', 'revoked']);
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldShowNotFoundGivenAnUnknownMember', async () => {
    // Arrange
    api.reply(`${base}/members/${userId}/access`, 404, problem(404, 'The member was not found.'));

    // Act
    const container = mount(() => <MemberAccessPage userId={userId} />);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Member not found'));

    // Assert
    expect(container.querySelector('a[href="/acme/members"]')).not.toBeNull();
  });
});
