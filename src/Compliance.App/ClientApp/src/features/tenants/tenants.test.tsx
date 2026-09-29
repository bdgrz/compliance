// @vitest-environment jsdom
import { resolveRouteRequest } from '@askrjs/askr/router';
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { pageRegistry } from '../../pages/_routes.js';
import { OrganizationBar, OrganizationUnavailable } from './organization-layout.js';
import { SelectTenantPage } from './pages/select-tenant.js';
import {
  chooseOrganizationEntry,
  clearActiveTenant,
  currentTenant,
  organizationPath,
  requireActiveTenantId,
  resolveTenantRoute,
} from './tenants.js';

const acme = { tenantId: '0190a1b2-0000-7000-8000-000000000001', name: 'Acme Corp', slug: 'acme' };
const beta = { tenantId: '0190a1b2-0000-7000-8000-000000000002', name: 'Beta LLC', slug: 'beta-co' };

let api: ReturnType<typeof stubApi>;

function reply(path: string, status: number, body?: unknown) {
  api.reply(path, status, body);
}

function memberships(...tenants: (typeof acme)[]) {
  reply('/api/v1/tenants/mine', 200, {
    items: tenants.map((t) => ({ tenant_id: t.tenantId, name: t.name, slug: t.slug })),
    next_cursor: null,
  });
}

function slugResolves(slug: string, tenant: typeof acme, redirect = false) {
  reply(`/api/v1/tenant-slugs/${slug}/mine`, 200, {
    tenant_id: tenant.tenantId,
    current_slug: tenant.slug,
    redirect,
  });
}

function at(pathname: string, search = '', hash = '') {
  return { pathname, search, hash };
}

const mounted: RenderResult[] = [];

beforeEach(() => {
  clearActiveTenant();
  api = stubApi();
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('organization entry (R1-15 frontend #176)', () => {
  it('ShouldEnterTheOnlyOrganizationGivenOneMembership', () => {
    expect(chooseOrganizationEntry([acme], null)).toEqual({ kind: 'enter', href: '/acme' });
  });

  it('ShouldEnterTheRememberedOrganizationGivenItIsStillAMembership', () => {
    expect(chooseOrganizationEntry([acme, beta], 'beta-co', '/teams')).toEqual({
      kind: 'enter',
      href: '/beta-co/teams',
    });
  });

  it('ShouldOfferTheSelectorGivenSeveralMembershipsAndAStaleRememberedSlug', () => {
    expect(chooseOrganizationEntry([acme, beta], 'gone-co')).toEqual({ kind: 'choose' });
  });

  it('ShouldReportNoAccessGivenNoMemberships', () => {
    expect(chooseOrganizationEntry([], 'acme')).toEqual({ kind: 'none' });
  });
});

describe('organization slug routes (R1-15 frontend #176)', () => {
  it('ShouldPreferStaticRoutesOverOrganizationSlugs', async () => {
    const auth = { authenticated: true } as never;

    const selector = (await resolveRouteRequest('/organizations', { registry: pageRegistry, authContext: auth })) as {
      params: Record<string, string>;
    };
    const teams = (await resolveRouteRequest('/acme/teams/team-1', { registry: pageRegistry, authContext: auth })) as {
      params: Record<string, string>;
    };

    expect(selector.params).toEqual({});
    expect(teams.params).toEqual({ slug: 'acme', teamId: 'team-1' });
  });

  it('ShouldActivateTheResolvedOrganizationGivenAMemberSlug', async () => {
    // Arrange
    slugResolves('acme', acme);
    memberships(acme, beta);

    // Act
    const resolution = await resolveTenantRoute('acme', at('/acme/teams'));

    // Assert
    expect(resolution).toEqual({ kind: 'ready', tenant: acme });
    expect(requireActiveTenantId()).toBe(acme.tenantId);
    expect(organizationPath('/teams')).toBe('/acme/teams');
  });

  it('ShouldRedirectToTheCurrentSlugGivenARenamedOrganization', async () => {
    // Arrange
    slugResolves('acme-old', acme, true);

    // Act
    const resolution = await resolveTenantRoute('acme-old', at('/acme-old/roles/r-1', '?tab=teams', '#top'));

    // Assert
    expect(resolution).toEqual({ kind: 'redirect', href: '/acme/roles/r-1?tab=teams#top' });
    expect(currentTenant()).toBeNull();
  });

  it('ShouldTreatUnknownAndDeniedSlugsIdenticallyGivenNoAccess', async () => {
    // Arrange
    reply('/api/v1/tenant-slugs/unknown-co/mine', 404);
    reply('/api/v1/tenant-slugs/secret-co/mine', 403);

    // Act
    const unknown = await resolveTenantRoute('unknown-co', at('/unknown-co'));
    const denied = await resolveTenantRoute('secret-co', at('/secret-co'));

    // Assert
    expect(unknown).toEqual({ kind: 'unavailable' });
    expect(denied).toEqual(unknown);
    expect(currentTenant()).toBeNull();
  });

  it('ShouldRejectMalformedSlugsWithoutCallingTheApi', async () => {
    expect(await resolveTenantRoute('Not_A_Slug', at('/Not_A_Slug'))).toEqual({ kind: 'unavailable' });
    expect(api.requested).toEqual([]);
  });

  it('ShouldOfferRetryGivenAServerFailure', async () => {
    // Arrange
    reply('/api/v1/tenant-slugs/acme/mine', 503);

    // Act
    const resolution = await resolveTenantRoute('acme', at('/acme'));

    // Assert
    expect(resolution.kind).toBe('failed');
  });

  it('ShouldReloadTheDocumentGivenAnotherOrganizationIsAlreadyLoaded', async () => {
    // Arrange
    slugResolves('acme', acme);
    slugResolves('beta-co', beta);
    memberships(acme, beta);
    await resolveTenantRoute('acme', at('/acme/teams'));

    // Act
    const switched = await resolveTenantRoute('beta-co', at('/beta-co/teams'));

    // Assert
    expect(switched).toEqual({ kind: 'reload', href: '/beta-co/teams' });
    expect(requireActiveTenantId()).toBe(acme.tenantId);
  });

  it('ShouldNotActivateAnOrganizationMissingFromTheUsersMemberships', async () => {
    // Arrange
    slugResolves('beta-co', beta);
    memberships(acme);

    // Act
    const resolution = await resolveTenantRoute('beta-co', at('/beta-co'));

    // Assert
    expect(resolution).toEqual({ kind: 'unavailable' });
    expect(() => requireActiveTenantId()).toThrow();
  });
});

describe('organization navigation accessibility (M0-D24)', () => {
  function mount(component: () => unknown): HTMLElement {
    const result = render(component as never);
    mounted.push(result);
    return result.container;
  }

  it('ShouldShowTheActiveOrganizationAndSlugMoveWithoutViolations', async () => {
    const container = mount(() => (
      <main>
        <OrganizationBar tenant={acme} movedFrom="acme-old" />
      </main>
    ));

    expect(container.textContent).toContain('Acme Corp');
    expect(container.querySelector('[role="status"]')?.textContent).toContain('/acme-old');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldExplainAnUnavailableOrganizationWithoutViolations', async () => {
    const container = mount(() => <OrganizationUnavailable />);

    expect(container.querySelector('h1')?.textContent).toBe('Organization unavailable');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldOfferCreationAndInvitationGuidanceGivenNoMemberships', async () => {
    // Arrange
    memberships();

    // Act
    const container = mount(() => <SelectTenantPage />);
    await vi.waitFor(() => expect(container.textContent).toContain('No organization access yet'));

    // Assert
    expect(container.textContent).toContain('requires a verified email address');
    expect(container.querySelector('a[href="/organizations/new"]')).not.toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldLinkEachMembershipToItsSlugRoute', async () => {
    // Arrange
    memberships(acme, beta);

    // Act
    const container = mount(() => <SelectTenantPage />);
    await vi.waitFor(() => expect(container.textContent).toContain('Beta LLC'));

    // Assert
    const hrefs = [...container.querySelectorAll('a')].map((a) => a.getAttribute('href'));
    expect(hrefs).toEqual(expect.arrayContaining(['/acme', '/beta-co']));
    expect(await accessibilityViolations(container)).toEqual([]);
  });
});
