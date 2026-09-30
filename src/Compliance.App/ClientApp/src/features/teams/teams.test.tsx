// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { TeamsListPage } from './pages/teams-list.js';

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
