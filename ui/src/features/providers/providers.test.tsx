// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { ProviderDetailPage } from './pages/provider-detail.js';
import { ProvidersPage } from './pages/providers-list.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const providerId = '0190a1b2-0000-7000-8000-0000000000b1';
const ownerId = '0190a1b2-0000-7000-8000-0000000000e1';
const applicationId = '0190a1b2-0000-7000-8000-0000000000a1';
const instanceId = '0190a1b2-0000-7000-8000-0000000000c1';
const base = `/api/v1/tenants/${tenantId}`;
const providerPath = `${base}/providers/${providerId}`;
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

const citation = {
  artifact_kind: 'soc2_report',
  title: 'Acme Cloud SOC 2 Type II',
  version_or_date: '2026-03-01',
  locator: 'https://trust.acmecloud.example/soc2',
  metadata_classification: 'public',
  artifact_id: null,
};

function providerBody(revision = 2, content: Record<string, unknown> = {}, unresolved: string[] = ['csocs']) {
  return {
    tenant_id: tenantId,
    provider_id: providerId,
    revision,
    content: {
      name: 'Acme Cloud',
      provider_kind: 'hosting',
      materiality: 'material',
      materiality_basis: ['customer_data', 'critical_path'],
      materiality_rationale: 'Hosts production customer data.',
      subservice: true,
      boundary_treatment: 'carve_out',
      boundary_treatment_rationale: null,
      owner_person_id: ownerId,
      owner_reference: null,
      source_citation: citation,
      dependencies: [
        {
          subject_kind: 'system_instance',
          subject_id: instanceId,
          program_id: null,
          application_id: applicationId,
          rationale: 'Runs the payroll production tenant.',
          effective_from: '2026-04-01T00:00:00Z',
          effective_until_exclusive: null,
          unresolved_reference: null,
          source_citation: null,
        },
        {
          subject_kind: 'client_service',
          subject_id: null,
          program_id: null,
          application_id: null,
          rationale: 'Backs the reporting service.',
          effective_from: '2026-04-01T00:00:00Z',
          effective_until_exclusive: null,
          unresolved_reference: 'Reporting service (not yet registered)',
          source_citation: null,
        },
      ],
      ...content,
    },
    source_kind: 'manual',
    lifecycle: 'active',
    unresolved,
    recorded_by: { kind: 'member', id: 'm1', display: 'Casey Lead' },
    recorded_at: '2026-09-20T00:00:00Z',
  };
}

function lookups() {
  api.reply(`${base}/people`, 200, {
    items: [
      {
        tenant_id: tenantId,
        person_id: ownerId,
        revision: 1,
        display_name: 'Riley Owner',
        work_email: 'riley@example.com',
        source_kind: 'manual',
        last_changed_by_member_id: 'm',
        last_changed_by_display: 'Casey Lead',
        last_changed_by: { kind: 'member', id: 'm', display: 'Casey Lead' },
        last_changed_at: '2026-09-01T00:00:00Z',
        correlated_user_id: null,
      },
    ],
    next_cursor: null,
  });
  api.reply(`${base}/applications`, 200, { items: [], next_cursor: null });
  api.reply(`${base}/client-services`, 200, { items: [], next_cursor: null });
}

function detailAnswers(revision = 2) {
  lookups();
  api.reply(`GET ${providerPath}`, 200, providerBody(revision));
  api.reply(`${providerPath}/revisions`, 200, {
    items: [providerBody(1, { materiality: null, materiality_basis: [], materiality_rationale: null }, ['materiality', 'csocs']), providerBody(2)],
    next_cursor: null,
  });
}

function field(container: HTMLElement, label: string): HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement {
  const found = [...container.querySelectorAll('label')].find((l) => l.textContent?.startsWith(label));
  if (!found) throw new Error(`No field labelled ${label}`);
  return found.querySelector('input,select,textarea') as HTMLInputElement;
}

function typeInto(container: HTMLElement, label: string, value: string) {
  const input = field(container, label);
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
  input.dispatchEvent(new Event('change', { bubbles: true }));
}

function toggle(container: HTMLElement, label: string) {
  field(container, label).click();
}

function submit(container: HTMLElement, label: string) {
  container.querySelector(`form[aria-label="${label}"]`)!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
}

beforeEach(async () => {
  clearActiveTenant();
  api = stubApi();
  api.reply('/api/v1/tenant-slugs/acme/mine', 200, { tenant_id: tenantId, current_slug: 'acme', redirect: false });
  api.reply('/api/v1/tenants/mine', 200, { items: [{ tenant_id: tenantId, name: 'Acme Corp', slug: 'acme' }], next_cursor: null });
  await resolveTenantRoute('acme', { pathname: '/acme/providers', search: '', hash: '' });
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('provider register list and record (R1-14 frontend #232)', () => {
  it('ShouldListProvidersWithMaterialityOwnerAndUnresolvedFacts', async () => {
    // Arrange
    lookups();
    const unclassified = providerBody(
      1,
      { name: 'Pixel Print', materiality: null, materiality_basis: [], owner_person_id: null, source_citation: null, subservice: false, boundary_treatment: null },
      ['materiality', 'owner', 'source_citation']
    );
    api.reply(`${base}/providers`, 200, { items: [providerBody(), { ...unclassified, provider_id: 'other' }], next_cursor: null });

    // Act
    const container = mount(ProvidersPage);
    await vi.waitFor(() => expect(container.textContent).toContain('Acme Cloud'));

    // Assert
    expect(container.querySelector(`a[href="/acme/providers/${providerId}"]`)).not.toBeNull();
    expect(container.textContent).toContain('Material');
    expect(container.textContent).toContain('Customer data');
    expect(container.textContent).toContain('Riley Owner');
    expect(container.textContent).toContain('Carved out');
    expect(container.textContent).toContain('CSOCs unresolved');
    expect(container.textContent).toContain('Materiality unresolved');
    expect(container.textContent).toContain('Owner unresolved');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldFollowCursorsGivenMoreThanOnePageOfProviders', async () => {
    // Arrange
    lookups();
    const inner = globalThis.fetch;
    const second = { ...providerBody(), provider_id: 'second', content: { ...providerBody().content, name: 'Second Provider' } };
    vi.stubGlobal('fetch', async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(input instanceof Request ? input.url : String(input), 'http://app.test');
      if (url.pathname === `${base}/providers`) {
        const page = url.searchParams.get('cursor') === 'next' ? { items: [second], next_cursor: null } : { items: [providerBody()], next_cursor: 'next' };
        return new Response(JSON.stringify(page), { status: 200, headers: { 'Content-Type': 'application/json' } });
      }
      return inner(input, init);
    });

    // Act
    const container = mount(ProvidersPage);
    await vi.waitFor(() => expect(container.textContent).toContain('Second Provider'));

    // Assert
    expect(container.textContent).toContain('Acme Cloud');
  });

  it('ShouldInviteRecordingGivenNoProviders', async () => {
    // Arrange
    lookups();
    api.reply(`${base}/providers`, 200, { items: [], next_cursor: null });

    // Act
    const container = mount(ProvidersPage);
    await vi.waitFor(() => expect(container.textContent).toContain('No providers yet'));

    // Assert
    expect(container.querySelector('form[aria-label="Record a provider"]')).not.toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldShowForbiddenGivenTheViewerCannotReadTheRegister', async () => {
    // Arrange
    lookups();
    api.reply(`${base}/providers`, 403, problem(403, 'Forbidden'));

    // Act
    const container = mount(ProvidersPage);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('The provider register is not available to you'));

    // Assert
    expect(container.querySelector('form')).toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldOfferRetryGivenTheListFails', async () => {
    // Arrange
    lookups();
    api.reply(`${base}/providers`, 500, problem(500, 'Boom'));

    // Act
    const container = mount(ProvidersPage);
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());
    api.reply(`${base}/providers`, 200, { items: [providerBody()], next_cursor: null });
    [...container.querySelectorAll('button')].find((b) => b.textContent === 'Try again')!.click();

    // Assert
    await vi.waitFor(() => expect(container.textContent).toContain('Acme Cloud'));
  });

  it('ShouldExplainProjectionLagGivenATransientConflict', async () => {
    // Arrange
    lookups();
    api.reply(`${base}/providers`, 409, problem(409, 'Projection is behind.', true));

    // Act
    const container = mount(ProvidersPage);
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.textContent).toContain('still being processed');
  });

  it('ShouldRecordAProviderWithMaterialityBasisAndCarveOutDefault', async () => {
    // Arrange
    lookups();
    api.reply(`${base}/providers`, 200, { items: [], next_cursor: null });
    api.reply(`POST ${base}/providers`, 400, problem(400, 'A provider requires a name.'));
    const container = mount(ProvidersPage);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Record a provider"]')).not.toBeNull());
    typeInto(container, 'Provider name', ' Acme Cloud ');
    typeInto(container, 'Provider kind', 'hosting');
    typeInto(container, 'Materiality', 'material');
    toggle(container, 'Customer data exposure');
    typeInto(container, 'Materiality rationale', 'Hosts production customer data.');
    toggle(container, 'Subservice organization');
    await vi.waitFor(() => expect(container.querySelector('option[value="' + ownerId + '"]')).not.toBeNull());
    typeInto(container, 'Accountable owner', ownerId);

    // Act
    submit(container, 'Record a provider');
    await vi.waitFor(() => expect(api.bodies.some((b) => b.method === 'POST')).toBe(true));

    // Assert
    const sent = api.bodies.find((b) => b.method === 'POST')?.body as { content: Record<string, unknown> };
    expect(sent.content).toMatchObject({
      name: 'Acme Cloud',
      provider_kind: 'hosting',
      materiality: 'material',
      materiality_basis: ['customer_data'],
      materiality_rationale: 'Hosts production customer data.',
      subservice: true,
      boundary_treatment: 'carve_out',
      owner_person_id: ownerId,
      dependencies: [],
      source_citation: null,
    });
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')?.textContent).toBe('A provider requires a name.'));
  });

  it('ShouldRequireARationaleGivenInclusiveTreatment', async () => {
    // Arrange
    lookups();
    api.reply(`${base}/providers`, 200, { items: [], next_cursor: null });
    const container = mount(ProvidersPage);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Record a provider"]')).not.toBeNull());
    toggle(container, 'Subservice organization');

    // Act
    typeInto(container, 'Boundary treatment', 'inclusive');

    // Assert
    await vi.waitFor(() => expect((field(container, 'Treatment rationale') as HTMLTextAreaElement).required).toBe(true));
  });

  it('ShouldNotOfferExposureBasisGivenNotMaterial', async () => {
    // Arrange
    lookups();
    api.reply(`${base}/providers`, 200, { items: [], next_cursor: null });
    const container = mount(ProvidersPage);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Record a provider"]')).not.toBeNull());
    typeInto(container, 'Materiality', 'material');
    await vi.waitFor(() => expect(container.textContent).toContain('Customer data exposure'));

    // Act
    typeInto(container, 'Materiality', 'not_material');

    // Assert
    await vi.waitFor(() => expect(container.textContent).not.toContain('Customer data exposure'));
  });

  it('ShouldShowForbiddenMessageGivenTheViewerCannotRecordProviders', async () => {
    // Arrange
    lookups();
    api.reply(`${base}/providers`, 200, { items: [], next_cursor: null });
    api.reply(`POST ${base}/providers`, 403, problem(403, 'Forbidden'));
    const container = mount(ProvidersPage);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Record a provider"]')).not.toBeNull());
    typeInto(container, 'Provider name', 'Acme Cloud');
    typeInto(container, 'Provider kind', 'hosting');

    // Act
    submit(container, 'Record a provider');
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('You do not have permission to record the provider');
  });
});

describe('provider detail, revisions and provenance (R1-14 frontend #232)', () => {
  it('ShouldShowFactsCitationDependenciesAndProvenance', async () => {
    // Arrange
    detailAnswers();

    // Act
    const container = mount(() => <ProviderDetailPage providerId={providerId} />);
    await vi.waitFor(() => expect(container.querySelectorAll('.provider-history > li')).toHaveLength(2));

    // Assert
    expect(container.querySelector('h1')?.textContent).toBe('Acme Cloud');
    expect(container.textContent).toContain('Riley Owner');
    expect(container.textContent).toContain('Customer data');
    expect(container.textContent).toContain('Critical path');
    expect(container.textContent).toContain('Hosts production customer data.');
    expect(container.textContent).toContain('Carved out');
    expect(container.textContent).toContain('CSOCs unresolved');
    expect(container.textContent).toContain('Acme Cloud SOC 2 Type II');
    expect(container.textContent).toContain('https://trust.acmecloud.example/soc2');
    expect(container.textContent).toContain('Public');
    expect(container.textContent).toContain('Recorded by Casey Lead');
    expect(container.querySelector(`a[href="/acme/applications/${applicationId}"]`)).not.toBeNull();
    expect(container.textContent).toContain('Reporting service (not yet registered)');
    expect(container.querySelector('.provider-history > li')?.textContent).toContain('Revision 2');
    expect(container.querySelector('a[href="https://trust.acmecloud.example/soc2"]')).toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldNotLinkArtifactContentGivenAGovernedArtifactReference', async () => {
    // Arrange
    lookups();
    api.reply(`GET ${providerPath}`, 200, providerBody(2, { source_citation: { ...citation, artifact_id: 'artifact-1', metadata_classification: 'internal' } }));
    api.reply(`${providerPath}/revisions`, 200, { items: [providerBody(2)], next_cursor: null });

    // Act
    const container = mount(() => <ProviderDetailPage providerId={providerId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Governed artifact reference'));

    // Assert
    expect(container.textContent).toContain('Internal');
    expect(container.querySelector('a[href*="artifact-1"]')).toBeNull();
  });

  it('ShouldReviseAgainstTheExpectedRevision', async () => {
    // Arrange
    detailAnswers(2);
    api.reply(`PUT ${providerPath}`, 200, { provider_id: providerId, revision: 3 });
    const container = mount(() => <ProviderDetailPage providerId={providerId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Revise provider"]')).not.toBeNull());
    typeInto(container, 'Materiality rationale', 'Hosts production and backup customer data.');

    // Act
    submit(container, 'Revise provider');
    await vi.waitFor(() => expect(api.bodies.some((b) => b.method === 'PUT')).toBe(true));

    // Assert
    const sent = api.bodies.find((b) => b.method === 'PUT')?.body as { expected_revision: number; content: Record<string, unknown> };
    expect(sent.expected_revision).toBe(2);
    expect(sent.content.materiality_rationale).toBe('Hosts production and backup customer data.');
    expect(sent.content.name).toBe('Acme Cloud');
    expect(sent.content.owner_person_id).toBe(ownerId);
    expect((sent.content.dependencies as unknown[]).length).toBe(2);
    await vi.waitFor(() => expect(api.requested.filter((p) => p === providerPath).length).toBeGreaterThan(1));
  });

  it('ShouldAskToReloadGivenAStaleRevisionConflict', async () => {
    // Arrange
    detailAnswers(2);
    api.reply(`PUT ${providerPath}`, 409, problem(409, 'The provider revision is stale; current revision is 3.'));
    const container = mount(() => <ProviderDetailPage providerId={providerId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Revise provider"]')).not.toBeNull());

    // Act
    submit(container, 'Revise provider');
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('Someone else changed this provider');
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Reload provider')).toBe(true);
  });

  it('ShouldShowForbiddenMessageGivenTheViewerCannotRevise', async () => {
    // Arrange
    detailAnswers(2);
    api.reply(`PUT ${providerPath}`, 403, problem(403, 'Forbidden'));
    const container = mount(() => <ProviderDetailPage providerId={providerId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Revise provider"]')).not.toBeNull());

    // Act
    submit(container, 'Revise provider');
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('You do not have permission to save the provider');
  });

  it('ShouldShowForbiddenGivenTheViewerCannotReadTheProvider', async () => {
    // Arrange
    lookups();
    api.reply(`GET ${providerPath}`, 403, problem(403, 'Forbidden'));

    // Act
    const container = mount(() => <ProviderDetailPage providerId={providerId} />);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('This provider is not available to you'));

    // Assert
    expect(container.querySelector('a[href="/acme/providers"]')).not.toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldShowNotFoundGivenAnUnknownProvider', async () => {
    // Arrange
    lookups();
    api.reply(`GET ${providerPath}`, 404, problem(404, 'The provider was not found.'));

    // Act
    const container = mount(() => <ProviderDetailPage providerId={providerId} />);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Provider not found'));

    // Assert
    expect(container.querySelector('a[href="/acme/providers"]')).not.toBeNull();
  });

  it('ShouldOfferRetryGivenTheProviderFailsToLoad', async () => {
    // Arrange
    lookups();
    api.reply(`GET ${providerPath}`, 409, problem(409, 'Projection is behind.', true));

    // Act
    const container = mount(() => <ProviderDetailPage providerId={providerId} />);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Provider could not be loaded'));

    // Assert
    expect(container.textContent).toContain('still being processed');
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Try again')).toBe(true);
  });

  it('ShouldRetryTheHistoryIndependentlyGivenItFailsToLoad', async () => {
    // Arrange
    lookups();
    api.reply(`GET ${providerPath}`, 200, providerBody(2));
    api.reply(`${providerPath}/revisions`, 500, problem(500, 'History failed.'));

    // Act
    const container = mount(() => <ProviderDetailPage providerId={providerId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('History failed.'));

    // Assert
    expect(container.querySelector('h1')?.textContent).toBe('Acme Cloud');
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Try again')).toBe(true);
  });

  it('ShouldRecordADependencyWithAnUnresolvedReference', async () => {
    // Arrange
    detailAnswers(2);
    api.reply(`PUT ${providerPath}`, 200, { provider_id: providerId, revision: 3 });
    const container = mount(() => <ProviderDetailPage providerId={providerId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Revise provider"]')).not.toBeNull());
    [...container.querySelectorAll('button')].find((b) => b.textContent === 'Add dependency')!.click();
    await vi.waitFor(() => expect(container.querySelectorAll('.provider-dependency-row').length).toBe(3));
    const row = container.querySelectorAll('.provider-dependency-row')[2] as HTMLElement;
    typeInto(row, 'Dependency rationale', 'Used for payroll exports.');
    typeInto(row, 'Unresolved reference', 'Export bucket');
    typeInto(row, 'Effective from', '2026-05-01');

    // Act
    submit(container, 'Revise provider');
    await vi.waitFor(() => expect(api.bodies.some((b) => b.method === 'PUT')).toBe(true));

    // Assert
    const sent = api.bodies.find((b) => b.method === 'PUT')?.body as { content: { dependencies: Record<string, unknown>[] } };
    expect(sent.content.dependencies[2]).toMatchObject({
      subject_kind: 'client_service',
      subject_id: null,
      rationale: 'Used for payroll exports.',
      unresolved_reference: 'Export bucket',
      effective_from: '2026-05-01T00:00:00Z',
    });
  });
});
