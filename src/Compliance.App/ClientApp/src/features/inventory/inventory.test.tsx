// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { InventoryPage } from './pages/inventory.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const base = `/api/v1/tenants/${tenantId}`;
const personId = '0190a1b2-0000-7000-8000-0000000000d1';
const appId = '0190a1b2-0000-7000-8000-0000000000e1';
const storeId = '0190a1b2-0000-7000-8000-0000000000e2';
const assetId = '0190a1b2-0000-7000-8000-0000000000f1';
const flowId = '0190a1b2-0000-7000-8000-0000000000f9';
const actor = { kind: 'member', id: 'm', display: 'Casey Lead' };
let api: ReturnType<typeof stubApi>;
const mounted: RenderResult[] = [];

function mount(component: () => unknown): HTMLElement {
  const result = render(component as never);
  mounted.push(result);
  return result.container;
}

function problem(status: number, detail: string, transient = false) {
  return { type: 'about:blank', title: 'Problem', status, detail, instance: '/', transient };
}

function component(id: string, name: string, category: string, revision = 1) {
  return {
    tenant_id: tenantId,
    component_id: id,
    revision,
    content: {
      category,
      name,
      owner_person_id: personId,
      environment_reference: 'prod',
      location_reference: null,
      system_instance_id: null,
      endpoint_count: null,
      management_source: null,
      lifecycle: 'active',
    },
    source_kind: 'manual',
    last_changed_by: actor,
    last_changed_at: '2026-09-10T00:00:00Z',
  };
}

const asset = {
  tenant_id: tenantId,
  information_asset_id: assetId,
  revision: 1,
  content: {
    name: 'Customer records',
    classification: 'confidential',
    retention_reference: 'RET-7',
    owner_person_id: personId,
    description: null,
    lifecycle: 'active',
  },
  source_kind: 'manual',
  last_changed_by: actor,
  last_changed_at: '2026-09-10T00:00:00Z',
};

const flow = {
  tenant_id: tenantId,
  data_flow_id: flowId,
  revision: 2,
  content: {
    source_type: 'technology_component',
    source_id: appId,
    destination_type: 'technology_component',
    destination_id: storeId,
    destination_party: null,
    information_asset_ids: [assetId],
    purpose: 'Persist orders',
    encrypted_in_transit: true,
    encrypted_at_rest: true,
    exception_reference: null,
    effective_from: '2026-09-01',
    owner_person_id: personId,
    lifecycle: 'active',
    classification: 'confidential',
  },
  source_kind: 'manual',
  last_changed_by: actor,
  last_changed_at: '2026-09-10T00:00:00Z',
};

function inventoryAnswers() {
  api.reply(`${base}/people`, 200, {
    items: [
      {
        tenant_id: tenantId,
        person_id: personId,
        revision: 1,
        display_name: 'Pat Owner',
        work_email: null,
        source_kind: 'manual',
        last_changed_by: actor,
        last_changed_at: '2026-09-01T00:00:00Z',
      },
    ],
    next_cursor: null,
  });
  api.reply(`${base}/technology-components`, 200, {
    items: [component(appId, 'Orders repo', 'repository'), component(storeId, 'Orders DB', 'data_store')],
    next_cursor: null,
  });
  api.reply(`${base}/information-assets`, 200, { items: [asset], next_cursor: null });
  api.reply(`${base}/data-flows`, 200, { items: [flow], next_cursor: null });
}

function press(container: HTMLElement, text: string) {
  const button = [...container.querySelectorAll('button')].find((b) => b.textContent === text)!;
  button.click();
}

function fill(form: Element, labelStart: string, value: string) {
  const field = [...form.querySelectorAll('label')]
    .find((l) => l.textContent?.startsWith(labelStart))!
    .querySelector('input, textarea, select') as HTMLInputElement;
  field.value = value;
  field.dispatchEvent(new Event(field.tagName === 'SELECT' ? 'change' : 'input', { bubbles: true }));
}

function submit(form: Element) {
  form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
}

beforeEach(async () => {
  clearActiveTenant();
  api = stubApi();
  api.reply('/api/v1/tenant-slugs/acme/mine', 200, { tenant_id: tenantId, current_slug: 'acme', redirect: false });
  api.reply('/api/v1/tenants/mine', 200, { items: [{ tenant_id: tenantId, name: 'Acme Corp', slug: 'acme' }], next_cursor: null });
  await resolveTenantRoute('acme', { pathname: '/acme/inventory', search: '', hash: '' });
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('technology and information inventory (R1-12 frontend #228)', () => {
  it('ShouldListComponentsWithOwnersAndSwitchSections', async () => {
    // Arrange
    inventoryAnswers();

    // Act
    const container = mount(InventoryPage);
    await vi.waitFor(() => expect(container.textContent).toContain('Orders DB'));

    // Assert
    expect(container.querySelector('h1')?.textContent).toBe('Inventory');
    expect(container.textContent).toContain('Data store · owner Pat Owner · Active');
    expect(await accessibilityViolations(container)).toEqual([]);
    press(container, 'Data flows');
    await vi.waitFor(() => expect(container.textContent).toContain('Persist orders'));
    expect(container.textContent).toContain('Orders repo → Orders DB');
    expect(container.textContent).toContain('carries Customer records (Confidential)');
    expect(container.textContent).toContain('in transit encrypted, at rest encrypted');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldRecordAnInformationAsset', async () => {
    // Arrange
    inventoryAnswers();
    api.reply(`POST ${base}/information-assets`, 200, { information_asset_id: assetId });
    const container = mount(InventoryPage);
    await vi.waitFor(() => expect(container.textContent).toContain('Orders DB'));
    press(container, 'Information assets');
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Record an information asset"]')).not.toBeNull());
    const form = container.querySelector('form[aria-label="Record an information asset"]')!;
    fill(form, 'Name', 'Payroll');
    fill(form, 'Classification', 'restricted');
    fill(form, 'Retention reference', 'RET-1');
    fill(form, 'Owner', personId);

    // Act
    submit(form);
    await vi.waitFor(() => expect(form.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(api.bodies.find((b) => b.method === 'POST' && b.path === `${base}/information-assets`)?.body).toEqual({
      name: 'Payroll',
      classification: 'restricted',
      retention_reference: 'RET-1',
      owner_person_id: personId,
      description: null,
    });
  });

  it('ShouldReviseAComponentAgainstTheExpectedRevision', async () => {
    // Arrange
    inventoryAnswers();
    api.reply(`PUT ${base}/technology-components/${storeId}`, 204);
    const container = mount(InventoryPage);
    await vi.waitFor(() => expect(container.textContent).toContain('Orders DB'));
    press(container, 'Revise Orders DB');
    const form = container.querySelector('form[aria-label="Revise Orders DB"]')!;
    fill(form, 'Lifecycle', 'retired');

    // Act
    submit(form);
    await vi.waitFor(() => expect(form.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(api.bodies.find((b) => b.method === 'PUT')?.body).toMatchObject({
      expected_revision: 1,
      name: 'Orders DB',
      owner_person_id: personId,
      lifecycle: 'retired',
    });
  });

  it('ShouldAskToReloadGivenAStaleFlowRevision', async () => {
    // Arrange
    inventoryAnswers();
    api.reply(`PUT ${base}/data-flows/${flowId}`, 409, problem(409, 'The data flow revision is stale.'));
    const container = mount(InventoryPage);
    await vi.waitFor(() => expect(container.textContent).toContain('Orders DB'));
    press(container, 'Data flows');
    await vi.waitFor(() => expect(container.textContent).toContain('Persist orders'));
    press(container, 'Revise Persist orders');
    const form = container.querySelector('form[aria-label="Revise data flow Persist orders"]')!;

    // Act
    submit(form);
    await vi.waitFor(() => expect(form.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(form.querySelector('[role="alert"]')?.textContent).toContain('Someone else changed this data flow');
    expect(api.bodies.find((b) => b.method === 'PUT')?.body).toMatchObject({ expected_revision: 2, information_asset_ids: [assetId] });
  });

  it('ShouldRequireAnExceptionGivenAnUnencryptedSensitiveFlow', async () => {
    // Arrange
    inventoryAnswers();
    const container = mount(InventoryPage);
    await vi.waitFor(() => expect(container.textContent).toContain('Orders DB'));
    press(container, 'Data flows');
    await vi.waitFor(() => expect(container.textContent).toContain('Persist orders'));
    const form = container.querySelector('form[aria-label="Record a data flow"]')!;
    const assetBox = [...form.querySelectorAll('label')]
      .find((l) => l.textContent?.includes('Customer records'))!
      .querySelector('input') as HTMLInputElement;

    // Act
    assetBox.checked = true;
    assetBox.dispatchEvent(new Event('change', { bubbles: true }));
    const atRest = [...form.querySelectorAll('label')]
      .find((l) => l.textContent?.includes('Encrypted at rest'))!
      .querySelector('input') as HTMLInputElement;
    atRest.checked = false;
    atRest.dispatchEvent(new Event('change', { bubbles: true }));

    // Assert
    await vi.waitFor(() => expect(form.textContent).toContain('Record an approved exception reference'));
  });

  it('ShouldShowRevisionHistoryForAnAsset', async () => {
    // Arrange
    inventoryAnswers();
    api.reply(`${base}/information-assets/${assetId}/revisions`, 200, {
      items: [asset, { ...asset, revision: 2, content: { ...asset.content, retention_reference: 'RET-9' } }],
      next_cursor: null,
    });
    const container = mount(InventoryPage);
    await vi.waitFor(() => expect(container.textContent).toContain('Orders DB'));
    press(container, 'Information assets');
    await vi.waitFor(() => expect(container.textContent).toContain('Customer records'));

    // Act
    press(container, 'History of Customer records');
    await vi.waitFor(() => expect(container.querySelectorAll('.inventory-history li')).toHaveLength(2));

    // Assert
    expect(container.querySelector('.inventory-history li')?.textContent).toContain('Revision 2');
    expect(container.querySelector('.inventory-history li')?.textContent).toContain('retention RET-9');
  });

  it('ShouldOfferRetryGivenProjectionLag', async () => {
    // Arrange
    inventoryAnswers();
    api.reply(`${base}/technology-components`, 409, problem(409, 'Projection is behind.', true));

    // Act
    const container = mount(InventoryPage);
    await vi.waitFor(() => expect(container.textContent).toContain('still being processed'));

    // Assert
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Try again')).toBe(true);
  });

  it('ShouldShowEmptyStatesGivenNoRecords', async () => {
    // Arrange
    inventoryAnswers();
    api.reply(`${base}/technology-components`, 200, { items: [], next_cursor: null });

    // Act
    const container = mount(InventoryPage);
    await vi.waitFor(() => expect(container.textContent).toContain('No technology components recorded yet.'));

    // Assert
    expect(container.querySelector('form[aria-label="Record a technology component"]')).not.toBeNull();
  });

  it('ShouldShowForbiddenGivenTheViewerCannotReadTheInventory', async () => {
    // Arrange
    for (const path of ['technology-components', 'information-assets', 'data-flows', 'people']) {
      api.reply(`${base}/${path}`, 403, problem(403, 'Forbidden'));
    }

    // Act
    const container = mount(InventoryPage);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('The inventory is not available to you'));

    // Assert
    expect(container.querySelector('form')).toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldPreviewAffectedFlowsBeforeChangingAnAssetClassification', async () => {
    // Arrange
    inventoryAnswers();
    api.reply(`POST ${base}/information-assets/${assetId}/change-previews`, 200, {
      tenant_id: tenantId,
      information_asset_id: assetId,
      revision: 1,
      current_classification: 'confidential',
      proposed_classification: 'restricted',
      current_lifecycle: 'active',
      proposed_lifecycle: 'active',
      affected_flows: [
        {
          data_flow_id: flowId,
          revision: 2,
          recorded_classification: 'confidential',
          recomputed_classification: 'restricted',
          classification_changed: true,
          encrypted_in_transit: true,
          encrypted_at_rest: false,
          exception_reference: null,
          encryption_violation: true,
          carries_retired_asset_only: false,
        },
      ],
      flows_over_limit: false,
    });
    api.reply(`PUT ${base}/information-assets/${assetId}`, 204);
    const container = mount(InventoryPage);
    await vi.waitFor(() => expect(container.textContent).toContain('Orders DB'));
    press(container, 'Information assets');
    await vi.waitFor(() => expect(container.textContent).toContain('Customer records'));
    press(container, 'Revise Customer records');
    const form = container.querySelector('form[aria-label="Revise Customer records"]')!;
    fill(form, 'Classification', 'restricted');

    // Act
    press(container, 'Preview impact');
    await vi.waitFor(() => expect(form.querySelector('.inventory-impact')).not.toBeNull());

    // Assert
    expect(api.bodies.find((b) => b.path.endsWith('/change-previews'))?.body).toEqual({
      expected_revision: 1,
      classification: 'restricted',
      lifecycle: 'active',
    });
    const impact = form.querySelector('.inventory-impact')!.textContent;
    expect(impact).toContain('Confidential → Restricted');
    expect(impact).toContain('Persist orders');
    expect(impact).toContain('needs encryption or an exception');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldPickASystemInstanceGivenACloudAccount', async () => {
    // Arrange
    inventoryAnswers();
    const applicationId = '0190a1b2-0000-7000-8000-0000000000a1';
    const instanceId = '0190a1b2-0000-7000-8000-0000000000a2';
    api.reply(`${base}/applications`, 200, {
      items: [
        {
          tenant_id: tenantId,
          application_id: applicationId,
          revision: 1,
          name: 'Payroll',
          purpose: 'Runs payroll',
          owner_reference: null,
          source_kind: 'manual',
          source_identifier: 'manual',
          has_system_instances: true,
          unresolved: [],
          last_changed_by_member_id: 'm',
          last_changed_by_display: 'Casey Lead',
          last_changed_at: '2026-09-20T00:00:00Z',
          classification: null,
          lifecycle: 'active',
          retirement: null,
        },
      ],
      next_cursor: null,
    });
    api.reply(`${base}/applications/${applicationId}/system-instances`, 200, {
      items: [
        {
          tenant_id: tenantId,
          application_id: applicationId,
          system_instance_id: instanceId,
          name: 'Payroll production',
          kind: 'production tenant',
          access_boundary_reference: null,
          source_kind: 'manual',
          source_identifier: null,
          unresolved: [],
          declared_by_member_id: 'm',
          declared_by_display: 'Casey Lead',
          declared_at: '2026-09-21T00:00:00Z',
          revision: 1,
          lifecycle: 'active',
          retirement: null,
        },
      ],
      next_cursor: null,
    });
    api.reply(`POST ${base}/technology-components`, 200, { component_id: storeId });
    const container = mount(InventoryPage);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Record a technology component"]')).not.toBeNull());
    const form = container.querySelector('form[aria-label="Record a technology component"]')!;
    fill(form, 'Category', 'cloud_account');
    fill(form, 'Name', 'AWS payroll');
    fill(form, 'Owner', personId);
    await vi.waitFor(() => expect(form.textContent).toContain('Payroll — Payroll production'));
    fill(form, 'System instance', instanceId);

    // Act
    submit(form);
    await vi.waitFor(() => expect(form.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(api.bodies.find((b) => b.method === 'POST' && b.path === `${base}/technology-components`)?.body).toMatchObject({
      category: 'cloud_account',
      system_instance_id: instanceId,
    });
    expect(await accessibilityViolations(container)).toEqual([]);
  });
});
