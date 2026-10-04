// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { OrganizationAddressCard } from './organization-address-card.js';
import { clearActiveTenant, resolveTenantRoute } from './tenants.js';

vi.mock('@askrjs/askr/router', async (importOriginal) => ({
  ...(await importOriginal<object>()),
  currentAuth: () => ({ authenticated: true, principal: { id: '0190a1b2-0000-7000-8000-0000000000aa' } }),
}));

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const userId = '0190a1b2-0000-7000-8000-0000000000aa';
const base = `/api/v1/tenants/${tenantId}`;
let api: ReturnType<typeof stubApi>;
const mounted: RenderResult[] = [];

function mount(): HTMLElement {
  const result = render(() => <OrganizationAddressCard />);
  mounted.push(result);
  return result.container;
}

function type(input: HTMLInputElement, value: string) {
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
}

beforeEach(async () => {
  clearActiveTenant();
  api = stubApi();
  api.reply('/api/v1/tenant-slugs/acme/mine', 200, {
    tenant_id: tenantId,
    current_slug: 'acme',
    redirect: false,
  });
  api.reply('/api/v1/tenants/mine', 200, {
    items: [{ tenant_id: tenantId, name: 'Acme Corp', slug: 'acme' }],
    next_cursor: null,
  });
  await resolveTenantRoute('acme', { pathname: '/acme', search: '', hash: '' });
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('organization address management (R1-15 frontend #539)', () => {
  it('ShouldAnnouncePermissionLoadingGivenTheOrganizationAddressIsBeingChecked', async () => {
    // Arrange
    const fallbackFetch = globalThis.fetch;
    let resolveAccess!: (response: Response) => void;
    vi.stubGlobal('fetch', (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(input instanceof Request ? input.url : String(input), 'http://app.test');
      if (url.pathname === `${base}/members/${userId}/access`) {
        return new Promise<Response>((resolve) => {
          resolveAccess = resolve;
        });
      }
      return fallbackFetch(input, init);
    });

    // Act
    const container = mount();

    // Assert
    expect(container.querySelector('[role="status"]')?.textContent)
      .toContain('Loading organization address settings');
    expect(await accessibilityViolations(container)).toEqual([]);

    resolveAccess(new Response(JSON.stringify({
      tenant_id: tenantId,
      user_id: userId,
      effective_permissions: ['tenant.rbac.manage'],
    }), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    await vi.waitFor(() => expect(container.querySelector('input[name="slug"]')).not.toBeNull());
  });

  it('ShouldChangeTheOrganizationSlugAndWaitForItsRedirectGivenAnOrgAdmin', async () => {
    // Arrange
    api.reply(`GET ${base}/members/${userId}/access`, 200, {
      tenant_id: tenantId,
      user_id: userId,
      effective_permissions: ['tenant.rbac.manage'],
    });
    api.reply(`POST ${base}/slug-changes`, 204);
    const nativeSetTimeout = window.setTimeout.bind(window);
    let redirectReady = false;
    vi.spyOn(window, 'setTimeout').mockImplementation(((callback: TimerHandler) => {
      if (!redirectReady) {
        redirectReady = true;
        api.reply('/api/v1/tenant-slugs/acme/mine', 200, {
          tenant_id: tenantId,
          current_slug: 'acme-new',
          redirect: true,
        });
      }
      return nativeSetTimeout(callback, 0);
    }) as typeof window.setTimeout);

    // Act
    const container = mount();
    await vi.waitFor(() => expect(container.querySelector('input[name="slug"]')).not.toBeNull());
    type(container.querySelector('input[name="slug"]') as HTMLInputElement, 'acme-new');
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(container.textContent).toContain('Address change requested'));

    // Assert
    expect(api.bodies.find((body) => body.method === 'POST' && body.path === `${base}/slug-changes`)?.body)
      .toEqual({ slug: 'acme-new' });
    expect(container.textContent).toContain('Members who open the old address will be redirected');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldHideAddressChangesGivenAMemberWithoutTenantManagementAccess', async () => {
    // Arrange
    api.reply(`GET ${base}/members/${userId}/access`, 200, {
      tenant_id: tenantId,
      user_id: userId,
      effective_permissions: ['program.manage'],
    });

    // Act
    const container = mount();
    await vi.waitFor(() => expect(api.requested).toContain(`${base}/members/${userId}/access`));
    await vi.waitFor(() => expect(container.querySelector('form')).toBeNull());
    await vi.waitFor(() => expect(container.textContent).toBe(''));

    // Assert
    expect(api.bodies.some((body) => body.method === 'POST' && body.path === `${base}/slug-changes`)).toBe(false);
  });

  it('ShouldHideAddressSettingsGivenTheServerForbidsMemberAccessLookup', async () => {
    // Arrange
    api.reply(`GET ${base}/members/${userId}/access`, 403, {
      type: 'about:blank',
      title: 'Forbidden',
      status: 403,
      detail: 'Forbidden',
      instance: '/',
    });

    // Act
    const container = mount();
    await vi.waitFor(() => expect(api.requested).toContain(`${base}/members/${userId}/access`));
    await vi.waitFor(() => expect(container.textContent).toBe(''));
    await vi.waitFor(() => expect(container.querySelector('form')).toBeNull());

    // Assert
    expect(container.textContent).toBe('');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldOfferRetryGivenTheMemberAccessLookupFails', async () => {
    // Arrange
    api.reply(`GET ${base}/members/${userId}/access`, 503, {
      type: 'about:blank',
      title: 'Unavailable',
      status: 503,
      detail: 'The member access service is unavailable.',
      instance: '/',
    });

    // Act
    const container = mount();
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')?.textContent)
      .toContain("Unable to load this member's access."));
    const requestsBeforeRetry = api.requested.filter((path) => path === `${base}/members/${userId}/access`).length;
    [...container.querySelectorAll('button')].find((button) => button.textContent?.includes('Try again'))?.click();
    await vi.waitFor(() => expect(api.requested.filter((path) => path === `${base}/members/${userId}/access`)
      .length).toBeGreaterThan(requestsBeforeRetry));

    // Assert
    expect(container.querySelector('input[name="slug"]')).toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });
});
