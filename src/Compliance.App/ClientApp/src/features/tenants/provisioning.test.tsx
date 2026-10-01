// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { CreateTenantPage } from './pages/create-tenant.js';
import { SelectTenantPage } from './pages/select-tenant.js';
import { clearActiveTenant } from './tenants.js';

const userId = '0190a1b2-0000-7000-8000-0000000000a1';
let api: ReturnType<typeof stubApi>;
const mounted: RenderResult[] = [];

function mount(component: () => unknown): HTMLElement {
  const result = render(component as never);
  mounted.push(result);
  return result.container;
}

function setField(container: Element, label: string, value: string) {
  const input = [...container.querySelectorAll('label')].find((l) => l.textContent?.startsWith(label))!.querySelector(
    'input'
  ) as HTMLInputElement;
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
}

function noMemberships() {
  api.reply('/api/v1/tenants/mine', 200, { items: [], next_cursor: null });
}

function emails(verified: boolean) {
  api.reply(`/api/v1/users/${userId}/email-addresses`, 200, {
    items: [{ user_id: userId, email_address: 'casey@acme.test', verified }],
    next_cursor: null,
  });
}

beforeEach(() => {
  clearActiveTenant();
  api = stubApi();
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('no-access state (R1-15 frontend #176)', () => {
  it('ShouldOfferCreationGivenAVerifiedEmail', async () => {
    // Arrange
    noMemberships();
    emails(true);

    // Act
    const container = mount(() => <SelectTenantPage userId={userId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Your email address is verified'));

    // Assert
    expect(container.querySelector('a[href="/organizations/new"]')).not.toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldExplainVerificationAndHideCreationGivenNoVerifiedEmail', async () => {
    // Arrange
    noMemberships();
    emails(false);

    // Act
    const container = mount(() => <SelectTenantPage userId={userId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('verify an email address you own'));

    // Assert
    expect(container.querySelector('a[href="/organizations/new"]')).toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });
});

describe('self-service organization creation (R1-15 frontend #176)', () => {
  it('ShouldRegisterWithALegalNameAndOpenTheOrganization', async () => {
    // Arrange
    const assign = vi.fn();
    vi.stubGlobal('location', { ...window.location, assign });
    api.reply('POST /api/v1/tenants', 200, { tenant_id: 't1', slug: 'acme' });
    const container = mount(CreateTenantPage);
    setField(container, 'Name', 'Acme');
    setField(container, 'Legal name', ' Acme Corporation Inc. ');
    setField(container, 'Slug', 'acme');

    // Act
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(assign).toHaveBeenCalledWith('/acme'));

    // Assert
    expect(api.bodies.find((b) => b.path === '/api/v1/tenants')?.body).toEqual({
      name: 'Acme',
      slug: 'acme',
      legal_name: 'Acme Corporation Inc.',
    });
    expect(container.textContent).toContain('first Org Admin');
  });

  it('ShouldExplainTheVerifiedEmailRequirementGivenForbidden', async () => {
    // Arrange
    api.reply('POST /api/v1/tenants', 403, {
      type: 'about:blank',
      title: 'Forbidden',
      status: 403,
      detail: 'Verify an email address you own before creating an organization.',
      instance: '/',
    });
    const container = mount(CreateTenantPage);
    setField(container, 'Name', 'Acme');
    setField(container, 'Legal name', 'Acme Corporation Inc.');
    setField(container, 'Slug', 'acme');

    // Act
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('Verify an email address you own');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldShowTheServerValidationReason', async () => {
    // Arrange
    api.reply('POST /api/v1/tenants', 409, {
      type: 'about:blank', title: 'Conflict', status: 409, detail: 'The slug is already taken.', instance: '/',
    });
    const container = mount(CreateTenantPage);
    setField(container, 'Name', 'Acme');
    setField(container, 'Legal name', 'Acme Corporation Inc.');
    setField(container, 'Slug', 'acme');

    // Act
    container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toBe('The slug is already taken.');
  });
});
