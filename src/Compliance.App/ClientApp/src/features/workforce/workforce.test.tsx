// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { reviewDateProblem } from './identity-fields.js';
import { PersonDetailPage } from './pages/person-detail.js';
import { WorkRelationshipDetailPage } from './pages/relationship-detail.js';
import { ServiceIdentitiesPage } from './pages/service-identities.js';
import { ServiceIdentityDetailPage } from './pages/service-identity-detail.js';
import { WorkforceObservationsPage } from './pages/workforce-observations.js';
import { WorkforceRosterPage } from './pages/workforce-roster.js';
import { latestReviewDate } from './workforce.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const alexId = '0190a1b2-0000-7000-8000-0000000000a1';
const blairId = '0190a1b2-0000-7000-8000-0000000000a2';
const relationshipId = '0190a1b2-0000-7000-8000-0000000000c1';
const identityId = '0190a1b2-0000-7000-8000-0000000000d1';
const teamId = '0190a1b2-0000-7000-8000-0000000000e1';
const base = `/api/v1/tenants/${tenantId}`;
const actor = { kind: 'member', id: 'm', display: 'Casey Lead' };
let api: ReturnType<typeof stubApi>;
let urls: string[];
const mounted: RenderResult[] = [];

function mount(component: () => unknown): HTMLElement {
  const result = render(component as never);
  mounted.push(result);
  return result.container;
}

function problem(status: number, detail: string, transient = false) {
  return { type: 'about:blank', title: 'Problem', status, detail, instance: '/', transient };
}

function person(personId: string, name: string, revision = 1) {
  return {
    tenant_id: tenantId,
    person_id: personId,
    revision,
    display_name: name,
    work_email: `${name.split(' ')[0]!.toLowerCase()}@acme.test`,
    source_kind: 'manual',
    last_changed_by: actor,
    last_changed_at: '2026-09-20T00:00:00Z',
  };
}

function relationship(overrides: Record<string, unknown> = {}) {
  return {
    tenant_id: tenantId,
    relationship_id: relationshipId,
    revision: 3,
    person_id: alexId,
    source_worker_id: 'W-100',
    worker_type: 'employee',
    lifecycle_status: 'active',
    start_date: '2026-01-05',
    end_date: null,
    department: 'Engineering',
    manager_person_id: null,
    sponsor_person_id: null,
    restricted_fields_redacted: true,
    source_kind: 'manual',
    last_changed_by: actor,
    last_changed_at: '2026-09-20T00:00:00Z',
    ...overrides,
  };
}

function identity(overrides: Record<string, unknown> = {}) {
  return {
    tenant_id: tenantId,
    service_identity_id: identityId,
    revision: 2,
    display_name: 'deploy-bot',
    identity_kind: 'bot',
    purpose: 'Deploys the production API.',
    environment: 'production',
    lifecycle_status: 'active',
    owner_kind: 'person',
    owner_id: alexId,
    review_by: '2027-03-01',
    source_kind: 'manual',
    unowned: false,
    unowned_reasons: [],
    last_changed_by: actor,
    last_changed_at: '2026-09-20T00:00:00Z',
    ...overrides,
  };
}

function peopleAnswer() {
  api.reply(`GET ${base}/people`, 200, {
    items: [person(alexId, 'Alex Rivera'), person(blairId, 'Blair Chen')],
    next_cursor: null,
  });
}

function field(container: HTMLElement, label: string): HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement {
  const match = [...container.querySelectorAll('label')].find((l) => l.textContent?.startsWith(label));
  if (!match) throw new Error(`No field labelled ${label}`);
  return match.querySelector('input, select, textarea')!;
}

function setField(container: HTMLElement, label: string, value: string) {
  const element = field(container, label);
  element.value = value;
  element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? 'change' : 'input', { bubbles: true }));
}

function submit(form: Element) {
  form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
}

function sent(method: string, path: string) {
  return api.bodies.find((b) => b.method === method && b.path === path)?.body as Record<string, unknown>;
}

beforeEach(async () => {
  clearActiveTenant();
  api = stubApi();
  api.reply('/api/v1/tenant-slugs/acme/mine', 200, { tenant_id: tenantId, current_slug: 'acme', redirect: false });
  api.reply('/api/v1/tenants/mine', 200, {
    items: [{ tenant_id: tenantId, name: 'Acme Corp', slug: 'acme' }],
    next_cursor: null,
  });
  await resolveTenantRoute('acme', { pathname: '/acme/workforce', search: '', hash: '' });
  // Record full request URLs so tests can assert query filters.
  urls = [];
  const stubbed = globalThis.fetch;
  vi.stubGlobal('fetch', (input: RequestInfo | URL, init?: RequestInit) => {
    urls.push(input instanceof Request ? input.url : String(input));
    return stubbed(input, init);
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('workforce roster (R1-11a frontend #220)', () => {
  it('ShouldListPeopleAndRelationshipsWithTheManagerRedacted', async () => {
    // Arrange
    peopleAnswer();
    api.reply(`GET ${base}/work-relationships`, 200, { items: [relationship()], next_cursor: null });

    // Act
    const container = mount(WorkforceRosterPage);
    await vi.waitFor(() => expect(container.textContent).toContain('W-100'));

    // Assert
    expect(container.querySelector('h1')?.textContent).toBe('Workforce');
    expect(container.querySelector(`a[href="/acme/workforce/people/${alexId}"]`)?.textContent).toBe('Alex Rivera');
    expect(container.querySelector(`a[href="/acme/workforce/relationships/${relationshipId}"]`)).not.toBeNull();
    expect(container.textContent).toContain('Manual roster (authoritative source)');
    expect(container.textContent).toContain('Managers are restricted');
    expect(container.querySelector('table')?.textContent).not.toContain('Manager');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldRecordAPersonAndReloadTheRoster', async () => {
    // Arrange
    api.reply(`GET ${base}/people`, 200, { items: [], next_cursor: null });
    api.reply(`GET ${base}/work-relationships`, 200, { items: [], next_cursor: null });
    api.reply(`POST ${base}/people`, 200, { person_id: alexId });
    const container = mount(WorkforceRosterPage);
    await vi.waitFor(() => expect(container.textContent).toContain('No people on the roster yet'));
    setField(container, 'Display name', 'Alex Rivera');
    setField(container, 'Work email', 'alex@acme.test');

    // Act
    submit(container.querySelector('form')!);
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')?.textContent).toContain('Alex Rivera'));

    // Assert
    expect(sent('POST', `${base}/people`)).toEqual({ display_name: 'Alex Rivera', work_email: 'alex@acme.test' });
    expect(api.requested.filter((p) => p === `${base}/people`).length).toBeGreaterThanOrEqual(3);
  });

  it('ShouldRecordARelationshipOmittingBlankOptionalTerms', async () => {
    // Arrange
    peopleAnswer();
    api.reply(`GET ${base}/work-relationships`, 200, { items: [], next_cursor: null });
    api.reply(`POST ${base}/work-relationships`, 200, { relationship_id: relationshipId });
    const container = mount(WorkforceRosterPage);
    await vi.waitFor(() => expect(container.querySelectorAll('form')).toHaveLength(2));
    const form = container.querySelectorAll('form')[1]!;
    setField(form as HTMLElement, 'Person', alexId);
    setField(form as HTMLElement, 'Source worker ID', ' W-100 ');
    setField(form as HTMLElement, 'Start date', '2026-01-05');

    // Act
    submit(form);
    await vi.waitFor(() => expect(form.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(sent('POST', `${base}/work-relationships`)).toEqual({
      person_id: alexId,
      source_worker_id: 'W-100',
      worker_type: 'employee',
      lifecycle_status: 'active',
      start_date: '2026-01-05',
      department: null,
    });
  });

  it('ShouldShowForbiddenGivenTheViewerCannotManageWorkforce', async () => {
    // Arrange
    api.reply(`GET ${base}/people`, 403, problem(403, 'Forbidden'));
    api.reply(`GET ${base}/work-relationships`, 403, problem(403, 'Forbidden'));

    // Act
    const container = mount(WorkforceRosterPage);
    await vi.waitFor(() =>
      expect(container.querySelector('h1')?.textContent).toBe('The workforce roster is not available to you')
    );

    // Assert
    expect(container.querySelector('form')).toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldOfferRetryGivenProjectionLag', async () => {
    // Arrange
    api.reply(`GET ${base}/people`, 409, problem(409, 'Projection is behind.', true));
    api.reply(`GET ${base}/work-relationships`, 200, { items: [], next_cursor: null });

    // Act
    const container = mount(WorkforceRosterPage);
    await vi.waitFor(() => expect(container.textContent).toContain('still being processed'));

    // Assert
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Try again')).toBe(true);
  });

  it('ShouldRevisePersonAgainstTheExpectedRevision', async () => {
    // Arrange
    api.reply(`GET ${base}/people/${alexId}`, 200, person(alexId, 'Alex Rivera', 4));
    api.reply(`PUT ${base}/people/${alexId}`, 204);
    const container = mount(() => <PersonDetailPage personId={alexId} />);
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());
    setField(container, 'Display name', 'Alex R. Rivera');

    // Act
    submit(container.querySelector('form')!);
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(sent('PUT', `${base}/people/${alexId}`)).toEqual({
      expected_revision: 4,
      display_name: 'Alex R. Rivera',
      work_email: 'alex@acme.test',
    });
  });

  it('ShouldAskToReloadGivenAStalePersonRevision', async () => {
    // Arrange
    api.reply(`GET ${base}/people/${alexId}`, 200, person(alexId, 'Alex Rivera', 4));
    api.reply(`PUT ${base}/people/${alexId}`, 409, problem(409, 'Stale revision.'));
    const container = mount(() => <PersonDetailPage personId={alexId} />);
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());

    // Act
    submit(container.querySelector('form')!);
    await vi.waitFor(() => expect(container.querySelector('form [role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('form [role="alert"]')?.textContent).toContain('Someone else changed this person');
  });

  it('ShouldShowNotFoundGivenAnUnknownPerson', async () => {
    // Arrange
    api.reply(`GET ${base}/people/${alexId}`, 404, problem(404, 'Person not found.'));

    // Act
    const container = mount(() => <PersonDetailPage personId={alexId} />);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Person not found'));

    // Assert
    expect(container.querySelector('a[href="/acme/workforce"]')).not.toBeNull();
  });

  it('ShouldShowTheManagerOnlyOnTheSingleRelationship', async () => {
    // Arrange
    peopleAnswer();
    api.reply(
      `GET ${base}/work-relationships/${relationshipId}`,
      200,
      relationship({ manager_person_id: blairId, restricted_fields_redacted: false })
    );

    // Act
    const container = mount(() => <WorkRelationshipDetailPage relationshipId={relationshipId} />);
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());

    // Assert
    const terms = container.querySelector('.workforce-terms')!.textContent!;
    expect(terms).toContain('Manager (restricted)Blair Chen');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldRecordALeaverAsEndedWithAnEndDate', async () => {
    // Arrange
    peopleAnswer();
    api.reply(`GET ${base}/work-relationships/${relationshipId}`, 200, relationship({ restricted_fields_redacted: false }));
    api.reply(`PUT ${base}/work-relationships/${relationshipId}`, 204);
    const container = mount(() => <WorkRelationshipDetailPage relationshipId={relationshipId} />);
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());
    setField(container, 'Lifecycle status', 'ended');
    setField(container, 'End date', '2026-09-30');

    // Act
    submit(container.querySelector('form')!);
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')?.textContent).toBe('Leaver recorded.'));

    // Assert
    expect(sent('PUT', `${base}/work-relationships/${relationshipId}`)).toMatchObject({
      expected_revision: 3,
      lifecycle_status: 'ended',
      end_date: '2026-09-30',
    });
  });
});

describe('joiners, movers, and leavers (R1-11b frontend #222)', () => {
  it('ShouldListObservationsAsComplianceWorkThatNeverGrantsAccess', async () => {
    // Arrange
    peopleAnswer();
    api.reply(`GET ${base}/workforce-observations`, 200, {
      items: [
        {
          tenant_id: tenantId,
          observation_id: 'o1',
          kind: 'mover',
          status: 'open',
          relationship_id: relationshipId,
          person_id: alexId,
          source_worker_id: 'W-100',
          relationship_revision: 2,
          effective_date: '2026-09-01',
          changed_fields: ['department'],
          observed_from: { kind: 'source', id: 'manual', display: 'Manual roster' },
          observed_at: '2026-09-02T00:00:00Z',
        },
      ],
      next_cursor: null,
    });

    // Act
    const container = mount(WorkforceObservationsPage);
    await vi.waitFor(() => expect(container.textContent).toContain('W-100'));

    // Assert
    expect(container.querySelector('[role="note"]')?.textContent).toContain('never grant');
    expect(container.querySelector('tbody')?.textContent).toContain('Mover');
    expect(container.querySelector('tbody')?.textContent).toContain('Alex Rivera');
    expect(container.querySelector('tbody')?.textContent).toContain('department');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldFilterObservationsByKind', async () => {
    // Arrange
    peopleAnswer();
    api.reply(`GET ${base}/workforce-observations`, 200, { items: [], next_cursor: null });
    const container = mount(WorkforceObservationsPage);
    await vi.waitFor(() => expect(container.textContent).toContain('No workforce changes observed yet.'));

    // Act
    setField(container, 'Show', 'leaver');
    await vi.waitFor(() => expect(container.textContent).toContain('No leaver observations.'));

    // Assert
    expect(urls.some((url) => url.includes('/workforce-observations') && url.includes('kind=leaver'))).toBe(true);
  });

  it('ShouldShowForbiddenGivenObservationsAreDenied', async () => {
    // Arrange
    api.reply(`GET ${base}/workforce-observations`, 403, problem(403, 'Forbidden'));

    // Act
    const container = mount(WorkforceObservationsPage);
    await vi.waitFor(() =>
      expect(container.querySelector('h1')?.textContent).toBe('Workforce changes are not available to you')
    );

    // Assert
    expect(container.querySelector('table')).toBeNull();
  });
});

describe('non-human identity owners (R1-11c frontend #224)', () => {
  it('ShouldListUnownedIdentitiesWithTheirReasons', async () => {
    // Arrange
    peopleAnswer();
    api.reply(`GET ${base}/teams`, 200, { items: [], next_cursor: null });
    api.reply(`GET ${base}/service-identities`, 200, {
      items: [identity({ unowned: true, unowned_reasons: ['review_expired', 'owner_relationship_ended'] })],
      next_cursor: null,
    });
    const container = mount(ServiceIdentitiesPage);
    await vi.waitFor(() => expect(container.textContent).toContain('deploy-bot'));

    // Act
    const toggle = container.querySelector('input[type="checkbox"]') as HTMLInputElement;
    toggle.checked = true;
    toggle.dispatchEvent(new Event('change', { bubbles: true }));
    await vi.waitFor(() => expect(urls.some((url) => url.includes('unowned_only=true'))).toBe(true));

    // Assert
    await vi.waitFor(() => expect(container.querySelector('.workforce-gaps')).not.toBeNull());
    expect(container.querySelector('.workforce-gaps')?.textContent).toContain('Ownership review date has passed');
    expect(container.querySelector('.workforce-gaps')?.textContent).toContain("owner's work relationship has ended");
    expect(container.textContent).toContain('owner Alex Rivera');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldRecordAnIdentityOwnedByATeam', async () => {
    // Arrange
    peopleAnswer();
    api.reply(`GET ${base}/teams`, 200, { items: [{ team_id: teamId, name: 'Platform' }], next_cursor: null });
    api.reply(`GET ${base}/service-identities`, 200, { items: [], next_cursor: null });
    api.reply(`POST ${base}/service-identities`, 200, { service_identity_id: identityId });
    const container = mount(ServiceIdentitiesPage);
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());
    const review = latestReviewDate();
    setField(container, 'Name', 'ci-runner');
    setField(container, 'Identity kind', 'automation');
    setField(container, 'Approved purpose', 'Runs CI.');
    setField(container, 'Owner type', 'team');
    await vi.waitFor(() => expect(field(container, 'Accountable owner').textContent).toContain('Platform'));
    setField(container, 'Accountable owner', teamId);
    setField(container, 'Review ownership by', review);

    // Act
    submit(container.querySelector('form')!);
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(sent('POST', `${base}/service-identities`)).toEqual({
      display_name: 'ci-runner',
      identity_kind: 'automation',
      purpose: 'Runs CI.',
      owner_kind: 'team',
      owner_id: teamId,
      review_by: review,
      environment: null,
    });
  });

  it('ShouldRejectAReviewDateMoreThanOneYearOut', () => {
    // Arrange
    const today = new Date(Date.UTC(2026, 8, 30));

    // Act
    const tooLate = reviewDateProblem('2027-10-01', today);
    const past = reviewDateProblem('2026-09-30', today);
    const fine = reviewDateProblem('2027-09-30', today);

    // Assert
    expect(tooLate).toContain('one year');
    expect(past).toContain('future');
    expect(fine).toBeNull();
  });

  it('ShouldReviseAnIdentityAgainstTheExpectedRevision', async () => {
    // Arrange
    peopleAnswer();
    api.reply(`GET ${base}/teams`, 200, { items: [], next_cursor: null });
    api.reply(`GET ${base}/service-identities/${identityId}`, 200, identity({ review_by: latestReviewDate() }));
    api.reply(`PUT ${base}/service-identities/${identityId}`, 204);
    const container = mount(() => <ServiceIdentityDetailPage serviceIdentityId={identityId} />);
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());
    setField(container, 'Lifecycle', 'disabled');

    // Act
    submit(container.querySelector('form')!);
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(sent('PUT', `${base}/service-identities/${identityId}`)).toMatchObject({
      expected_revision: 2,
      lifecycle_status: 'disabled',
      owner_kind: 'person',
      owner_id: alexId,
    });
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldShowTheOwnershipGapOnAnUnownedIdentity', async () => {
    // Arrange
    peopleAnswer();
    api.reply(`GET ${base}/teams`, 200, { items: [], next_cursor: null });
    api.reply(
      `GET ${base}/service-identities/${identityId}`,
      200,
      identity({ unowned: true, unowned_reasons: ['owner_not_on_roster'] })
    );

    // Act
    const container = mount(() => <ServiceIdentityDetailPage serviceIdentityId={identityId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Ownership gap'));

    // Assert
    expect(container.textContent).toContain('The owner is not on the workforce roster');
  });
});
