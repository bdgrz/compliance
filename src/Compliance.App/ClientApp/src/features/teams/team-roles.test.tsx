// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { TeamRolesPanel } from './pages/team-roles-panel.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const teamId = 'team-sec';
const base = `/api/v1/tenants/${tenantId}`;
let api: ReturnType<typeof stubApi>;
const mounted: RenderResult[] = [];

function mount(component: () => unknown): HTMLElement {
  const result = render(component as never);
  mounted.push(result);
  return result.container;
}

function roles() {
  api.reply(`${base}/roles`, 200, {
    items: [
      { role_id: 'role-lead', name: 'Compliance Lead' },
      { role_id: 'role-contrib', name: 'Contributor' },
    ],
    next_cursor: null,
  });
  api.reply(`${base}/roles/role-lead/teams`, 200, { items: [{ team_id: teamId }], next_cursor: null });
  api.reply(`${base}/roles/role-contrib/teams`, 200, { items: [{ team_id: 'other-team' }], next_cursor: null });
}

beforeEach(async () => {
  clearActiveTenant();
  api = stubApi();
  api.reply('/api/v1/tenant-slugs/acme/mine', 200, { tenant_id: tenantId, current_slug: 'acme', redirect: false });
  api.reply('/api/v1/tenants/mine', 200, {
    items: [{ tenant_id: tenantId, name: 'Acme Corp', slug: 'acme' }],
    next_cursor: null,
  });
  await resolveTenantRoute('acme', { pathname: `/acme/teams/${teamId}`, search: '', hash: '' });
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('team role grants (R1-04c frontend #189)', () => {
  it('ShouldExplainInheritedAccessAndListOnlyThisTeamsRoles', async () => {
    // Arrange
    roles();

    // Act
    const container = mount(() => <TeamRolesPanel teamId={teamId} />);
    await vi.waitFor(() => expect(container.querySelector('.team-role')).not.toBeNull());

    // Assert
    expect(container.textContent).toContain('Every current member of this team gets the permissions');
    expect([...container.querySelectorAll('.team-role')].map((li) => li.textContent)).toEqual([
      'Compliance LeadRemove',
    ]);
    const options = [...container.querySelectorAll('option')].map((o) => o.textContent);
    expect(options).toEqual(['Choose a role', 'Contributor']);
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldGrantTheChosenRoleAndRefresh', async () => {
    // Arrange
    roles();
    api.reply(`POST ${base}/teams/${teamId}/roles/role-contrib`, 204);
    const container = mount(() => <TeamRolesPanel teamId={teamId} />);
    await vi.waitFor(() => expect(container.querySelector('select')).not.toBeNull());
    const select = container.querySelector('select') as HTMLSelectElement;
    select.value = 'role-contrib';
    select.dispatchEvent(new Event('change', { bubbles: true }));

    // Act
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(api.bodies.some((b) => b.method === 'POST' && b.path === `${base}/teams/${teamId}/roles/role-contrib`)).toBe(true);
    expect(container.querySelector('[role="status"]')?.textContent).toBe('Granted Contributor to this team.');
  });

  it('ShouldStateTheAccessLossGivenARoleIsRemoved', async () => {
    // Arrange
    roles();
    api.reply(`DELETE ${base}/teams/${teamId}/roles/role-lead`, 204);
    const container = mount(() => <TeamRolesPanel teamId={teamId} />);
    await vi.waitFor(() => expect(container.querySelector('.team-role button')).not.toBeNull());

    // Act
    (container.querySelector('.team-role button') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(api.bodies.some((b) => b.method === 'DELETE' && b.path === `${base}/teams/${teamId}/roles/role-lead`)).toBe(true);
    expect(container.querySelector('[role="status"]')?.textContent).toContain('lost the access it provided');
  });

  it('ShouldExplainForbiddenGivenTheViewerCannotManageTeams', async () => {
    // Arrange
    roles();
    api.reply(`DELETE ${base}/teams/${teamId}/roles/role-lead`, 403, {
      type: 'about:blank', title: 'Forbidden', status: 403, detail: 'Forbidden', instance: '/',
    });
    const container = mount(() => <TeamRolesPanel teamId={teamId} />);
    await vi.waitFor(() => expect(container.querySelector('.team-role button')).not.toBeNull());

    // Act
    (container.querySelector('.team-role button') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toBe('You do not have permission to change this.');
  });

  it('ShouldOfferRetryGivenTheTeamRolesFailedToLoad', async () => {
    // Arrange
    api.reply(`${base}/roles`, 503);

    // Act
    const container = mount(() => <TeamRolesPanel teamId={teamId} />);
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Try again')).toBe(true);
  });
});
