// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { PersonDetailPage } from './pages/person-detail.js';
import { RosterSnapshotDetailPage } from './pages/roster-snapshot-detail.js';
import { RosterSnapshotsPage } from './pages/roster-snapshots.js';
import { ServiceIdentitiesPage } from './pages/service-identities.js';
import { ServiceIdentityDetailPage } from './pages/service-identity-detail.js';
import { WorkforceObservationsPage } from './pages/workforce-observations.js';
import { WorkforceReconciliationPage } from './pages/workforce-reconciliation.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const alexId = '0190a1b2-0000-7000-8000-0000000000a1';
const blairId = '0190a1b2-0000-7000-8000-0000000000a2';
const relationshipId = '0190a1b2-0000-7000-8000-0000000000c1';
const identityId = '0190a1b2-0000-7000-8000-0000000000d1';
const userId = '0190a1b2-0000-7000-8000-0000000000b1';
const firmUserId = '0190a1b2-0000-7000-8000-0000000000b2';
const observationId = '0190a1b2-0000-7000-8000-0000000000e1';
const snapshotId = '0190a1b2-0000-7000-8000-0000000000f1';
const amendmentId = '0190a1b2-0000-7000-8000-0000000000f2';
const base = `/api/v1/tenants/${tenantId}`;
const actor = { kind: 'member', id: 'm', display: 'Casey Lead' };
const hash = 'ab12cd34ef56'.padEnd(64, '0');
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

function person(personId: string, name: string, extra: Record<string, unknown> = {}) {
  return {
    tenant_id: tenantId,
    person_id: personId,
    revision: 4,
    display_name: name,
    work_email: null,
    source_kind: 'manual',
    last_changed_by: actor,
    last_changed_at: '2026-09-20T00:00:00Z',
    ...extra,
  };
}

function people() {
  api.reply(`GET ${base}/people`, 200, { items: [person(alexId, 'Alex Rivera'), person(blairId, 'Blair Chen')], next_cursor: null });
}

function members() {
  api.reply(`GET ${base}/members`, 200, {
    items: [
      { user_id: userId, tenant_id: tenantId, affiliation: 'client_personnel', is_suspended: false, verified_email_address: 'alex@acme.test' },
      { user_id: firmUserId, tenant_id: tenantId, affiliation: 'firm_staff', is_suspended: false, verified_email_address: 'auditor@firm.test' },
    ],
    next_cursor: null,
  });
}

function finding(extra: Record<string, unknown> = {}) {
  return {
    tenant_id: tenantId,
    observation_id: observationId,
    kind: 'conflicting',
    reason: 'ended_worker_retains_access',
    status: 'open',
    person_ids: [alexId],
    relationship_ids: [relationshipId],
    user_ids: [userId],
    resolution: null,
    ...extra,
  };
}

function identity(extra: Record<string, unknown> = {}) {
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
    expires_on: null,
    expired: false,
    ...extra,
  };
}

function snapshot(extra: Record<string, unknown> = {}) {
  return {
    tenant_id: tenantId,
    snapshot_id: snapshotId,
    root_snapshot_id: snapshotId,
    amends_snapshot_id: null,
    content_sha256: hash,
    row_count: 3,
    amendment_reason: null,
    frozen_by: actor,
    frozen_at: '2026-09-25T12:00:00Z',
    people: [
      { person_id: alexId, revision: 4, display_name: 'Alex Rivera', work_email: 'alex@acme.test' },
      { person_id: blairId, revision: 1, display_name: 'Blair Chen', work_email: null },
    ],
    work_relationships: [
      {
        relationship_id: relationshipId, revision: 3, person_id: alexId, source_worker_id: 'W-100', worker_type: 'employee',
        lifecycle_status: 'active', start_date: '2026-01-05', end_date: null, department: 'Engineering',
        manager_person_id: blairId, sponsor_person_id: null,
      },
    ],
    restricted_fields_redacted: false,
    ...extra,
  };
}

function field(container: Element, label: string) {
  const match = [...container.querySelectorAll('label')].find((l) => l.textContent?.startsWith(label));
  if (!match) throw new Error(`No field labelled ${label}`);
  return match.querySelector('input, select, textarea') as HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement;
}

function setField(container: Element, label: string, value: string) {
  const element = field(container, label);
  element.value = value;
  element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? 'change' : 'input', { bubbles: true }));
}

function submit(form: Element) {
  form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
}

function sent(method: string, path: string) {
  return api.bodies.find((b) => b.method === method && b.path === path)?.body as Record<string, unknown> | undefined;
}

function button(container: Element, text: string) {
  return [...container.querySelectorAll('button')].find((b) => b.textContent === text) as HTMLButtonElement | undefined;
}

beforeEach(async () => {
  clearActiveTenant();
  api = stubApi();
  api.reply('/api/v1/tenant-slugs/acme/mine', 200, { tenant_id: tenantId, current_slug: 'acme', redirect: false });
  api.reply('/api/v1/tenants/mine', 200, { items: [{ tenant_id: tenantId, name: 'Acme Corp', slug: 'acme' }], next_cursor: null });
  await resolveTenantRoute('acme', { pathname: '/acme/workforce', search: '', hash: '' });
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

describe('roster reconciliation (R1-11 frontend b #484)', () => {
  it('ShouldListOpenFindingsWithTheirSubjectsAndSourcePrecedence', async () => {
    // Arrange
    people();
    members();
    api.reply(`GET ${base}/workforce-reconciliation-observations`, 200, { items: [finding()], next_cursor: null });

    // Act
    const container = mount(WorkforceReconciliationPage);
    await vi.waitFor(() => expect(container.textContent).toContain('the member still has access'));

    // Assert
    expect(urls.some((url) => url.includes('workforce-reconciliation-observations') && url.includes('status=open'))).toBe(true);
    expect(container.querySelector(`a[href="/acme/workforce/people/${alexId}"]`)?.textContent).toBe('Alex Rivera');
    expect(container.querySelector(`a[href="/acme/members/${userId}"]`)?.textContent).toBe('alex@acme.test');
    expect(container.textContent).toContain('Manual roster (in use)');
    expect(container.textContent).toContain('Accepted source provenance');
    expect(container.textContent).toContain('explicit canonical correction');
    expect(container.textContent).not.toContain('Which source wins');
    expect(container.querySelector('a[href="/acme/workforce/sources"]')).not.toBeNull();
    expect(container.textContent).toContain('never grants, changes, or revokes');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldResolveAFindingWithANoteAndReload', async () => {
    // Arrange
    people();
    members();
    api.reply(`GET ${base}/workforce-reconciliation-observations`, 200, { items: [finding()], next_cursor: null });
    api.reply(`PUT ${base}/workforce-observations/${observationId}/resolution`, 204);
    const container = mount(WorkforceReconciliationPage);
    await vi.waitFor(() => expect(container.querySelector('form.workforce-resolve')).not.toBeNull());
    const form = container.querySelector('form.workforce-resolve')!;
    setField(form, 'Outcome', 'dismissed');
    setField(form, 'Note', ' Contractor kept access for handover. ');

    // Act
    submit(form);
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')?.textContent).toContain('Dismissed'));

    // Assert
    expect(sent('PUT', `${base}/workforce-observations/${observationId}/resolution`)).toEqual({
      resolution: 'dismissed',
      note: 'Contractor kept access for handover.',
    });
    expect(api.requested.filter((p) => p === `${base}/workforce-reconciliation-observations`).length).toBeGreaterThanOrEqual(2);
  });

  it('ShouldRequireANoteBeforeClosing', async () => {
    // Arrange
    people();
    members();
    api.reply(`GET ${base}/workforce-reconciliation-observations`, 200, { items: [finding()], next_cursor: null });
    const container = mount(WorkforceReconciliationPage);
    await vi.waitFor(() => expect(container.querySelector('form.workforce-resolve')).not.toBeNull());

    // Act
    submit(container.querySelector('form.workforce-resolve')!);
    await vi.waitFor(() => expect(container.querySelector('form.workforce-resolve [role="alert"]')).not.toBeNull());

    // Assert
    expect(api.bodies.some((b) => b.method === 'PUT')).toBe(false);
  });

  it('ShouldShowTheAttributedClosureGivenAResolvedFinding', async () => {
    // Arrange
    people();
    members();
    api.reply(`GET ${base}/workforce-reconciliation-observations`, 200, {
      items: [
        finding({
          status: 'resolved',
          resolution: {
            tenant_id: tenantId, observation_id: observationId, resolution: 'resolved', note: 'Access removed.',
            resolved_by: actor, resolved_at: '2026-09-26T00:00:00Z',
          },
        }),
      ],
      next_cursor: null,
    });
    const container = mount(WorkforceReconciliationPage);
    await vi.waitFor(() => expect(container.querySelector('select')).not.toBeNull());

    // Act
    setField(container, 'Status', 'resolved');
    await vi.waitFor(() => expect(container.textContent).toContain('Access removed.'));

    // Assert
    expect(urls.some((url) => url.includes('status=resolved'))).toBe(true);
    expect(container.querySelector('.workforce-resolution')?.textContent).toContain('Resolved by Casey Lead');
    expect(container.querySelector('form.workforce-resolve')).toBeNull();
  });

  it('ShouldShowForbiddenGivenTheViewerCannotManageWorkforce', async () => {
    // Arrange
    api.reply(`GET ${base}/workforce-reconciliation-observations`, 403, problem(403, 'Forbidden'));

    // Act
    const container = mount(WorkforceReconciliationPage);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Roster reconciliation is not available to you'));

    // Assert
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldOfferRetryGivenProjectionLag', async () => {
    // Arrange
    api.reply(`GET ${base}/workforce-reconciliation-observations`, 409, problem(409, 'Behind.', true));

    // Act
    const container = mount(WorkforceReconciliationPage);
    await vi.waitFor(() => expect(container.textContent).toContain('still being processed'));

    // Assert
    expect(button(container, 'Try again')).toBeDefined();
  });

  it('ShouldCloseAJoinerMoverLeaverObservation', async () => {
    // Arrange
    people();
    api.reply(`GET ${base}/workforce-observations`, 200, {
      items: [
        {
          tenant_id: tenantId, observation_id: observationId, kind: 'leaver', status: 'open', relationship_id: relationshipId,
          person_id: alexId, source_worker_id: 'W-100', relationship_revision: 3, effective_date: '2026-09-30',
          changed_fields: ['lifecycle_status'], observed_from: actor, observed_at: '2026-09-25T00:00:00Z', resolution: null,
        },
      ],
      next_cursor: null,
    });
    api.reply(`PUT ${base}/workforce-observations/${observationId}/resolution`, 204);
    const container = mount(WorkforceObservationsPage);
    await vi.waitFor(() => expect(container.querySelector('form.workforce-resolve')).not.toBeNull());
    const form = container.querySelector('form.workforce-resolve')!;
    setField(form, 'Note', 'Accounts disabled.');

    // Act
    submit(form);
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(sent('PUT', `${base}/workforce-observations/${observationId}/resolution`)).toEqual({
      resolution: 'resolved',
      note: 'Accounts disabled.',
    });
    expect(await accessibilityViolations(container)).toEqual([]);
  });
});

describe('membership correlation (R1-11 frontend b #484)', () => {
  it('ShouldRehydratePersonEditorGivenSavedRevisionAfterProjectionLagRetry', async () => {
    // Arrange
    members();
    api.reply(`GET ${base}/people/${alexId}`, 200, person(alexId, 'Alex Rivera'));
    api.reply(`GET ${base}/workforce-source-observations`, 200, { items: [], next_cursor: null });
    api.reply(`PUT ${base}/people/${alexId}`, 204);
    const container = mount(() => <PersonDetailPage personId={alexId} />);
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());
    const form = container.querySelector('form')!;
    setField(form, 'Display name', 'Alex Rivera Corrected');
    api.reply(`GET ${base}/people/${alexId}`, 409, problem(409, 'Projection is catching up', true));

    // Act
    submit(form);
    await vi.waitFor(() => expect(button(container, 'Try again')).toBeDefined());
    api.reply(`GET ${base}/people/${alexId}`, 200, person(alexId, 'Alex Rivera Corrected', { revision: 5 }));
    button(container, 'Try again')!.click();
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Alex Rivera Corrected'));

    // Assert
    const editor = container.querySelector('form')!;
    expect(editor.querySelector<HTMLInputElement>('input[type="text"]')?.value).toBe('Alex Rivera Corrected');
    submit(editor);
    await vi.waitFor(() => expect(api.bodies.filter((entry) => entry.method === 'PUT')).toHaveLength(2));
    expect(api.bodies.filter((entry) => entry.method === 'PUT')[1].body).toMatchObject({
      expected_revision: 5,
      display_name: 'Alex Rivera Corrected',
    });
  });

  it('ShouldCorrelateAPersonWithAWorkforceMemberOnly', async () => {
    // Arrange
    api.reply(`GET ${base}/people/${alexId}`, 200, person(alexId, 'Alex Rivera'));
    members();
    api.reply(`PUT ${base}/people/${alexId}/membership-correlation`, 204);
    const container = mount(() => <PersonDetailPage personId={alexId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Correlate with a member"]')).not.toBeNull());
    const form = container.querySelector('form[aria-label="Correlate with a member"]')!;
    const options = [...field(form, 'Organization member').querySelectorAll('option')].map((o) => o.textContent);
    setField(form, 'Organization member', userId);

    // Act
    submit(form);
    await vi.waitFor(() => expect(form.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(options).toContain('alex@acme.test');
    expect(options).not.toContain('auditor@firm.test');
    expect(sent('PUT', `${base}/people/${alexId}/membership-correlation`)).toEqual({ expected_revision: 4, user_id: userId });
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldClearACorrelationWithoutAUserId', async () => {
    // Arrange
    api.reply(`GET ${base}/people/${alexId}`, 200, person(alexId, 'Alex Rivera', { correlated_user_id: userId }));
    members();
    api.reply(`PUT ${base}/people/${alexId}/membership-correlation`, 204);
    const container = mount(() => <PersonDetailPage personId={alexId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Correlated with'));
    const form = container.querySelector('form[aria-label="Correlate with a member"]')!;
    setField(form, 'Organization member', '');

    // Act
    submit(form);
    await vi.waitFor(() => expect(form.querySelector('[role="status"]')?.textContent).toContain('cleared'));

    // Assert
    expect(sent('PUT', `${base}/people/${alexId}/membership-correlation`)).toEqual({ expected_revision: 4 });
  });

  it('ShouldExplainAStaleConflictOnCorrelation', async () => {
    // Arrange
    api.reply(`GET ${base}/people/${alexId}`, 200, person(alexId, 'Alex Rivera'));
    members();
    api.reply(`PUT ${base}/people/${alexId}/membership-correlation`, 409, problem(409, 'Stale.'));
    const container = mount(() => <PersonDetailPage personId={alexId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Correlate with a member"]')).not.toBeNull());
    const form = container.querySelector('form[aria-label="Correlate with a member"]')!;
    setField(form, 'Organization member', userId);

    // Act
    submit(form);
    await vi.waitFor(() => expect(form.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(form.querySelector('[role="alert"]')?.textContent).toContain('Someone else changed this person');
  });
});

describe('non-human identity expiry (R1-11 frontend b #484)', () => {
  it('ShouldFlagExpiredIdentitiesInTheList', async () => {
    // Arrange
    people();
    api.reply(`GET ${base}/teams`, 200, { items: [], next_cursor: null });
    api.reply(`GET ${base}/service-identities`, 200, {
      items: [identity({ expires_on: '2026-09-01', expired: true })],
      next_cursor: null,
    });

    // Act
    const container = mount(ServiceIdentitiesPage);
    await vi.waitFor(() => expect(container.textContent).toContain('deploy-bot'));

    // Assert
    expect(container.querySelector('.workforce-expired-tag')?.textContent).toContain('Expired');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldReviseTheExpiryDate', async () => {
    // Arrange
    people();
    api.reply(`GET ${base}/teams`, 200, { items: [], next_cursor: null });
    api.reply(`GET ${base}/service-identities/${identityId}`, 200, identity({ expires_on: '2026-09-01', expired: true }));
    api.reply(`PUT ${base}/service-identities/${identityId}`, 204);
    const container = mount(() => <ServiceIdentityDetailPage serviceIdentityId={identityId} />);
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());
    const form = container.querySelector('form')!;
    setField(form, 'Credential expires on', '2027-01-31');

    // Act
    submit(form);
    await vi.waitFor(() => expect(form.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('.workforce-expired')?.textContent).toContain('expired');
    expect(sent('PUT', `${base}/service-identities/${identityId}`)).toMatchObject({ expires_on: '2027-01-31' });
  });
});

describe('workforce snapshots (R1-11d frontend #226)', () => {
  it.each([409, 503])('ShouldNotShowACompleteFreezeGivenFailureStatus%sAndAllowRetry', async (status) => {
    // Arrange
    api.reply(`GET ${base}/workforce-roster-snapshots`, 200, { items: [], next_cursor: null });
    api.reply(`POST ${base}/workforce-roster-snapshots`, status, problem(status, 'Freeze unavailable', status === 409));
    const container = mount(RosterSnapshotsPage);
    expect(container.querySelector('[role="progressbar"]')).not.toBeNull();
    await vi.waitFor(() => expect(container.textContent).toContain('No workforce snapshots frozen yet'));

    // Act
    button(container, 'Freeze roster now')!.click();
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(container.textContent).not.toContain('Roster frozen.');
    expect(container.textContent).not.toContain('Your snapshot is saved.');
    expect(container.querySelector(`a[href="/acme/workforce/snapshots/${snapshotId}"]`)).toBeNull();
    expect(button(container, 'Freeze roster now')!.disabled).toBe(false);
    api.reply(`POST ${base}/workforce-roster-snapshots`, 200, { snapshot_id: snapshotId, content_sha256: hash });
    button(container, 'Freeze roster now')!.click();
    await vi.waitFor(() => expect(container.querySelector(`a[href="/acme/workforce/snapshots/${snapshotId}"]`)).not.toBeNull());
    expect(container.querySelector('[role="alert"]')).toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldRetainSavedIdentityAndRefreshHistoryGivenLaggingSnapshotProjection', async () => {
    // Arrange
    api.reply(`GET ${base}/workforce-roster-snapshots`, 200, { items: [], next_cursor: null });
    api.reply(`POST ${base}/workforce-roster-snapshots`, 200, { snapshot_id: snapshotId, content_sha256: hash });
    const container = mount(RosterSnapshotsPage);
    await vi.waitFor(() => expect(container.textContent).toContain('No workforce snapshots frozen yet'));

    // Act
    button(container, 'Freeze roster now')!.click();
    await vi.waitFor(() => expect(container.querySelector(`a[href="/acme/workforce/snapshots/${snapshotId}"]`)).not.toBeNull());

    // Assert
    expect(container.textContent).toContain('Your snapshot is saved. Snapshot history is still updating.');
    expect(container.textContent).not.toContain('No workforce snapshots frozen yet');
    api.reply(`GET ${base}/workforce-roster-snapshots`, 503, problem(503, 'History unavailable'));
    button(container, 'Refresh snapshot history')!.click();
    await vi.waitFor(() => expect(button(container, 'Try again')).toBeDefined());
    expect(container.querySelector(`a[href="/acme/workforce/snapshots/${snapshotId}"]`)).not.toBeNull();
    api.reply(`GET ${base}/workforce-roster-snapshots`, 200, { items: [snapshot()], next_cursor: null });
    button(container, 'Try again')!.click();
    await vi.waitFor(() => expect(container.querySelectorAll('tbody tr')).toHaveLength(1));
    expect(container.textContent).not.toContain('Snapshot history is still updating');
    expect(button(container, 'Refresh snapshot history')).toBeUndefined();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldListSnapshotsWithLineageAndFreezeANewOne', async () => {
    // Arrange
    api.reply(`GET ${base}/workforce-roster-snapshots`, 200, {
      items: [
        { ...snapshot(), snapshot_id: amendmentId, amends_snapshot_id: snapshotId, kind: 'amendment', amendment_reason: 'Late leaver' },
        { ...snapshot(), kind: 'original' },
      ],
      next_cursor: null,
    });
    api.reply(`POST ${base}/workforce-roster-snapshots`, 200, { snapshot_id: amendmentId, content_sha256: hash });
    const container = mount(RosterSnapshotsPage);
    await vi.waitFor(() => expect(container.querySelectorAll('tbody tr')).toHaveLength(2));

    // Act
    button(container, 'Freeze roster now')!.click();
    await vi.waitFor(() => expect(container.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    const rows = [...container.querySelectorAll('tbody tr')].map((row) => row.textContent);
    expect(rows[0]).toContain('Amendment');
    expect(rows[0]).toContain('Late leaver');
    expect(container.querySelector(`[role="status"] a[href="/acme/workforce/snapshots/${amendmentId}"]`)).not.toBeNull();
    expect(api.requested.filter((p) => p === `${base}/workforce-roster-snapshots`).length).toBeGreaterThanOrEqual(3);
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldFindTheSnapshotInForceAtAPastTime', async () => {
    // Arrange
    api.reply(`GET ${base}/workforce-roster-snapshots`, 200, { items: [], next_cursor: null });
    api.reply(`GET ${base}/workforce-roster-snapshot-as-of`, 200, snapshot());
    const container = mount(RosterSnapshotsPage);
    await vi.waitFor(() => expect(container.textContent).toContain('No workforce snapshots frozen yet'));
    const form = container.querySelector('form[aria-label="Find the roster as of a time"]')!;
    setField(form, 'Roster as of', '2026-09-26T09:30');

    // Act
    submit(form);
    await vi.waitFor(() => expect(form.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(urls.some((url) => url.includes('workforce-roster-snapshot-as-of?as_of='))).toBe(true);
    expect(form.querySelector('[role="status"]')?.textContent).toContain('2 people');
  });

  it('ShouldExplainWhenNoSnapshotExistedAtThatTime', async () => {
    // Arrange
    api.reply(`GET ${base}/workforce-roster-snapshots`, 200, { items: [], next_cursor: null });
    api.reply(`GET ${base}/workforce-roster-snapshot-as-of`, 404, problem(404, 'No roster snapshot was frozen at or before that time.'));
    const container = mount(RosterSnapshotsPage);
    await vi.waitFor(() => expect(container.querySelector('form')).not.toBeNull());
    const form = container.querySelector('form[aria-label="Find the roster as of a time"]')!;
    setField(form, 'Roster as of', '2020-01-01T00:00');

    // Act
    submit(form);
    await vi.waitFor(() => expect(form.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(form.querySelector('[role="alert"]')?.textContent).toContain('No roster snapshot was frozen');
  });

  it('ShouldShowForbiddenGivenTheViewerCannotReadSnapshots', async () => {
    // Arrange
    api.reply(`GET ${base}/workforce-roster-snapshots`, 403, problem(403, 'Forbidden'));

    // Act
    const container = mount(RosterSnapshotsPage);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Workforce snapshots are not available to you'));

    // Assert
    expect(container.querySelector('button')).toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldShowFrozenRowsAndManagersGivenAccessToRestrictedFields', async () => {
    // Arrange
    api.reply(`GET ${base}/workforce-roster-snapshots/${snapshotId}`, 200, snapshot());

    // Act
    const container = mount(() => <RosterSnapshotDetailPage snapshotId={snapshotId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('W-100'));

    // Assert
    expect(container.querySelector('h1')?.textContent).toContain(hash.slice(0, 12));
    expect(container.textContent).toContain(hash);
    expect(container.textContent).toContain('Original snapshot');
    const headers = [...container.querySelectorAll('th')].map((th) => th.textContent);
    expect(headers).toContain('Manager');
    expect(container.textContent).toContain('Blair Chen');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldWithholdManagersGivenRestrictedFieldsAreRedacted', async () => {
    // Arrange
    api.reply(`GET ${base}/workforce-roster-snapshots/${amendmentId}`, 200, {
      ...snapshot({ snapshot_id: amendmentId, amends_snapshot_id: snapshotId, amendment_reason: 'Late leaver', restricted_fields_redacted: true }),
      work_relationships: [{ ...snapshot().work_relationships[0], manager_person_id: null }],
    });

    // Act
    const container = mount(() => <RosterSnapshotDetailPage snapshotId={amendmentId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('W-100'));

    // Assert
    expect([...container.querySelectorAll('th')].map((th) => th.textContent)).not.toContain('Manager');
    expect(container.textContent).toContain('withheld from you');
    expect(container.querySelector(`a[href="/acme/workforce/snapshots/${snapshotId}"]`)).not.toBeNull();
    expect(container.textContent).toContain('Late leaver');
  });

  it('ShouldAmendWithAReasonLeavingTheOriginalUnchanged', async () => {
    // Arrange
    api.reply(`GET ${base}/workforce-roster-snapshots/${snapshotId}`, 200, snapshot());
    api.reply(`POST ${base}/workforce-roster-snapshots/${snapshotId}/amendments`, 200, { snapshot_id: amendmentId, content_sha256: hash });
    const container = mount(() => <RosterSnapshotDetailPage snapshotId={snapshotId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Amend snapshot"]')).not.toBeNull());
    const form = container.querySelector('form[aria-label="Amend snapshot"]')!;
    setField(form, 'Reason for the amendment', ' Late leaver recorded ');

    // Act
    submit(form);
    await vi.waitFor(() => expect(form.querySelector('[role="status"]')).not.toBeNull());

    // Assert
    expect(sent('POST', `${base}/workforce-roster-snapshots/${snapshotId}/amendments`)).toEqual({ reason: 'Late leaver recorded' });
    expect(form.querySelector(`a[href="/acme/workforce/snapshots/${amendmentId}"]`)).not.toBeNull();
  });

  it('ShouldShowNotFoundGivenAnUnknownSnapshot', async () => {
    // Arrange
    api.reply(`GET ${base}/workforce-roster-snapshots/${snapshotId}`, 404, problem(404, 'The roster snapshot was not found.'));

    // Act
    const container = mount(() => <RosterSnapshotDetailPage snapshotId={snapshotId} />);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Workforce snapshot not found'));

    // Assert
    expect(container.querySelector('a[href="/acme/workforce/snapshots"]')).not.toBeNull();
  });

  it('ShouldExplainAnIntegrityConflictAndOfferRetry', async () => {
    // Arrange
    api.reply(
      `GET ${base}/workforce-roster-snapshots/${snapshotId}`,
      409,
      problem(409, 'The stored roster snapshot failed integrity verification.')
    );

    // Act
    const container = mount(() => <RosterSnapshotDetailPage snapshotId={snapshotId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('failed integrity verification'));

    // Assert
    expect(button(container, 'Try again')).toBeDefined();
  });
});
