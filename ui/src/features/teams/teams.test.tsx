// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { TeamsListPage } from './pages/teams-list.js';
import { TeamDetailPage } from './pages/team-detail.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
let api: ReturnType<typeof stubApi>;
const mounted: RenderResult[] = [];

beforeEach(async () => {
  clearActiveTenant();
  api = stubApi();
  api.reply('/api/v1/tenant-slugs/acme/mine', 200, { tenant_id: tenantId, current_slug: 'acme', redirect: false });
  api.reply('/api/v1/tenants/mine', 200, {
    items: [{ tenant_id: tenantId, name: 'Acme Corp', slug: 'acme' }],
    next_cursor: null,
  });
  await resolveTenantRoute('acme', { pathname: '/acme/teams', search: '', hash: '' });
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('teams in an organization (R1-15 frontend #176)', () => {
  function teamAnswer() {
    const base = `/api/v1/tenants/${tenantId}`;
    api.reply(`${base}/teams/team-1`, 200, { team_id: 'team-1', name: 'Security' });
    api.reply(`${base}/teams/team-1/roles`, 200, { items: [], next_cursor: null });
    api.reply(`${base}/roles`, 200, { items: [], next_cursor: null });
    return `${base}/teams/team-1/members`;
  }

  function mountDetail() {
    const result = render(() => <TeamDetailPage teamId="team-1" />);
    mounted.push(result);
    return result.container;
  }

  it('ShouldIdentifyTeamMembersGivenAuthorizedPresentation', async () => {
    // Arrange
    api.reply(teamAnswer(), 200, { items: [{ team_id: 'team-1', member_id: 'member-1',
      user_id: 'user-1', display_name: 'Ada Lovelace', verified_email_address: 'ada@example.test' }], next_cursor: null });

    // Act
    const container = mountDetail();

    // Assert
    await vi.waitFor(() => expect(container.textContent).toContain('Ada Lovelace'));
    expect(container.textContent).toContain('ada@example.test');
    expect(container.querySelector('a[href="/acme/members/user-1"]')?.textContent).toBe('Ada Lovelace');
    expect(container.querySelector('button[aria-label="Remove Ada Lovelace from this team"]')).not.toBeNull();
  });

  it('ShouldExplainEmptyPopulationGivenNoTeamMembers', async () => {
    // Arrange
    api.reply(teamAnswer(), 200, { items: [], next_cursor: null });

    // Act
    const container = mountDetail();

    // Assert
    await vi.waitFor(() => expect(container.textContent).toContain('No members belong to this team yet.'));
  });

  it('ShouldRetryMemberProjectionConflictGivenRefresh', async () => {
    // Arrange
    const path = teamAnswer();
    api.reply(path, 409, { detail: 'The member projection has not reached the source.', status: 409 });
    const container = mountDetail();
    await vi.waitFor(() => expect(container.textContent).toContain('The member projection'));
    api.reply(path, 200, { items: [{ team_id: 'team-1', member_id: 'member-1', display_name: 'Ada Lovelace' }], next_cursor: null });

    // Act
    [...container.querySelectorAll('button')].find(button => button.textContent?.trim() === 'Try again')!.click();

    // Assert
    await vi.waitFor(() => expect(container.textContent).toContain('Ada Lovelace'));
    expect(container.textContent).not.toContain('The member projection');
  });

  it('ShouldHideManagementGivenForbiddenMemberReads', async () => {
    // Arrange
    api.reply(teamAnswer(), 403, { detail: 'Forbidden', status: 403 });

    // Act
    const container = mountDetail();

    // Assert
    await vi.waitFor(() => expect(container.textContent).toContain('Team members are not available to you'));
    expect(container.querySelector('input[placeholder="Member ID"]')).toBeNull();
  });

  it('ShouldLoadTheOrganizationsTeamsOnceGivenARender', async () => {
    // Arrange
    const teamsPath = `/api/v1/tenants/${tenantId}/teams`;
    api.reply(teamsPath, 200, { items: [{ team_id: 'team-1', name: 'Security' }], next_cursor: null });

    // Act
    const result = render(TeamsListPage as never);
    mounted.push(result);
    await vi.waitFor(() => expect(result.container.textContent).toContain('Security'));
    await new Promise((resolve) => setTimeout(resolve, 100));

    // Assert
    expect(api.requested.filter((path) => path === teamsPath)).toHaveLength(1);
    expect(result.container.querySelector('a[href="/acme/teams/team-1"]')).not.toBeNull();
  });
});
