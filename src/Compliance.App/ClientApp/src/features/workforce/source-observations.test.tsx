// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { WorkforceSourcesPage } from './pages/workforce-sources.js';
import { WorkforceSourceDetailPage } from './pages/workforce-source-detail.js';
import { PersonDetailPage } from './pages/person-detail.js';
import { WorkRelationshipDetailPage } from './pages/relationship-detail.js';
import { ServiceIdentityDetailPage } from './pages/service-identity-detail.js';
import { SourceObservationsPanel } from './source-record.js';
import { listSourceObservations } from './source-observations.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const personId = '0190a1b2-0000-7000-8000-0000000000a1';
const observationId = '0190a1b2-0000-7000-8000-0000000000e1';
const relationshipId = '0190a1b2-0000-7000-8000-0000000000c1';
const identityId = '0190a1b2-0000-7000-8000-0000000000d1';
const base = `/api/v1/tenants/${tenantId}/workforce-source-observations`;
const actor = { kind: 'member', id: 'm', display: 'Casey Lead' };
let api: ReturnType<typeof stubApi>;
const mounted: RenderResult[] = [];

function observation(extra: Record<string, unknown> = {}) {
  return {
    tenant_id: tenantId,
    observation_id: observationId,
    revision: 2,
    source: {
      source_kind: 'hris',
      source_system: 'Payroll',
      source_record_id: 'W-100',
      source_revision: 'export-7',
    },
    target_kind: 'person',
    target_id: personId,
    observed_target_revision: 4,
    facts: {
      person: { display_name: 'Alex Rivera', work_email: 'alex@acme.test' },
    },
    observed_at: '2026-09-20T00:00:00Z',
    recorded_by: actor,
    recorded_at: '2026-09-21T00:00:00Z',
    decision: {
      outcome: 'accepted',
      note: 'Checked roster',
      target_revision: 4,
      actor,
      decided_at: '2026-09-22T00:00:00Z',
    },
    ...extra,
  };
}

function mount(component: () => unknown) {
  const result = render(component as never);
  mounted.push(result);
  return result.container;
}

function setField(container: Element, label: string, value: string) {
  const match = [...container.querySelectorAll('label')].find((item) =>
    item.textContent?.startsWith(label)
  );
  const input = match?.querySelector('input, select, textarea') as
    | HTMLInputElement
    | HTMLSelectElement
    | HTMLTextAreaElement;
  if (!input) throw new Error(`No field labelled ${label}`);
  input.value = value;
  input.dispatchEvent(
    new Event(input instanceof HTMLSelectElement ? 'change' : 'input', {
      bubbles: true,
    })
  );
}

function button(container: Element, label: string) {
  return [...container.querySelectorAll('button')].find(
    (item) => item.textContent === label
  )!;
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
    items: [
      { tenant_id: tenantId, slug: 'acme', name: 'Acme', kind: 'client' },
    ],
    next_cursor: null,
  });
  await resolveTenantRoute('acme', {
    pathname: '/acme/workforce/sources',
    search: '',
    hash: '',
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
  for (const result of mounted.splice(0)) result.cleanup();
});

it('ShouldListSourceIdentityAndHistoricalProvenanceWithoutClaimingCurrentAcceptance', async () => {
  api.reply(base, 200, { items: [observation()], next_cursor: null });

  const container = mount(WorkforceSourcesPage);

  await vi.waitFor(() => expect(container.textContent).toContain('Payroll'));
  expect(container.textContent).toContain('HRIS');
  expect(container.textContent).toContain('W-100');
  expect(container.textContent).toContain('export-7');
  expect(container.textContent).toContain('Accepted for revision 4');
  expect(container.textContent).not.toContain(
    'Accepted for the current revision'
  );
  expect(
    container.querySelector(
      `a[href="/acme/workforce/sources/${observationId}"]`
    )
  ).not.toBeNull();
  expect(api.requested).toContain(base);
});

function preview(extra: Record<string, unknown> = {}) {
  return {
    observation_id: observationId,
    revision: 1,
    source: observation().source,
    target_kind: 'person',
    target_id: personId,
    observed_target_revision: 4,
    current_target_revision: 5,
    source_authority: 'authoritative',
    conflicting_fields: ['display_name'],
    restricted_fields_conflict: false,
    can_accept: false,
    accepted_for_current_revision: false,
    decision: null,
    ...extra,
  };
}

it('ShouldRequireExplicitCanonicalCorrectionGivenConflictingSourceFacts', async () => {
  api.reply(
    `${base}/${observationId}`,
    200,
    observation({ revision: 1, decision: null })
  );
  api.reply(`${base}/${observationId}/preview`, 200, preview());

  const container = mount(() => WorkforceSourceDetailPage({ observationId }));

  await vi.waitFor(() =>
    expect(container.textContent).toContain('display name')
  );
  expect(container.textContent).toContain('Canonical revision 5');
  const accept = [...container.querySelectorAll('button')].find(
    (b) => b.textContent === 'Accept matching source'
  );
  expect(accept?.disabled).toBe(true);
  expect(
    [
      ...container.querySelectorAll(
        `a[href="/acme/workforce/people/${personId}"]`
      ),
    ].some((link) => link.textContent === 'Correct canonical record')
  ).toBe(true);
  expect(container.textContent).toContain(
    'never changes canonical facts or access'
  );
  expect(api.bodies.filter((b) => b.method !== 'GET')).toHaveLength(0);
});

it('ShouldAcceptMatchingFactsAtThePreviewedSourceAndCanonicalRevisions', async () => {
  api.reply(
    `${base}/${observationId}`,
    200,
    observation({ revision: 1, decision: null })
  );
  api.reply(
    `${base}/${observationId}/preview`,
    200,
    preview({ conflicting_fields: [], can_accept: true })
  );
  api.reply(`PUT ${base}/${observationId}/decision`, 204);
  const container = mount(() => WorkforceSourceDetailPage({ observationId }));
  await vi.waitFor(() =>
    expect(container.textContent).toContain('Observed facts match')
  );

  setField(container, 'Decision note', 'Checked against the HRIS roster.');
  await vi.waitFor(() =>
    expect(button(container, 'Accept matching source').disabled).toBe(false)
  );
  api.reply(
    `${base}/${observationId}/preview`,
    200,
    preview({
      revision: 2,
      conflicting_fields: [],
      accepted_for_current_revision: true,
      decision: {
        outcome: 'accepted',
        note: 'Checked against the HRIS roster.',
        target_revision: 5,
        actor,
        decided_at: '2026-09-23T00:00:00Z',
      },
    })
  );
  button(container, 'Accept matching source').click();

  await vi.waitFor(() =>
    expect(container.textContent).toContain(
      'Accepted for the current revision 5'
    )
  );
  expect(api.bodies.find((item) => item.method === 'PUT')?.body).toEqual({
    expected_revision: 1,
    expected_target_revision: 5,
    outcome: 'accepted',
    note: 'Checked against the HRIS roster.',
  });
  expect(container.textContent).toContain('Casey Lead');
  expect(container.textContent).toContain('Checked against the HRIS roster.');
  expect(container.querySelector('textarea')).toBeNull();
});

it('ShouldDismissConflictingEvidenceWithAnAttributedReasonAndKeepCanonicalFacts', async () => {
  api.reply(
    `${base}/${observationId}`,
    200,
    observation({ revision: 1, decision: null })
  );
  api.reply(`${base}/${observationId}/preview`, 200, preview());
  api.reply(`PUT ${base}/${observationId}/decision`, 204);
  const container = mount(() => WorkforceSourceDetailPage({ observationId }));
  await vi.waitFor(() =>
    expect(container.textContent).toContain('display name')
  );
  expect(button(container, 'Dismiss and keep canonical').disabled).toBe(true);
  setField(
    container,
    'Decision note',
    'Confirmed canonical spelling with workforce lead.'
  );
  await vi.waitFor(() =>
    expect(button(container, 'Dismiss and keep canonical').disabled).toBe(false)
  );
  button(container, 'Dismiss and keep canonical').click();

  await vi.waitFor(() =>
    expect(api.bodies.some((item) => item.method === 'PUT')).toBe(true)
  );
  expect(api.bodies.find((item) => item.method === 'PUT')?.body).toMatchObject({
    expected_revision: 1,
    expected_target_revision: 5,
    outcome: 'dismissed',
    note: 'Confirmed canonical spelling with workforce lead.',
  });
  expect(api.bodies.filter((item) => item.method !== 'GET')).toHaveLength(1);
});

it('ShouldRecordImmutablePersonEvidenceCorrelatedToTheOpenedCanonicalRevision', async () => {
  api.reply(`/api/v1/tenants/${tenantId}/people/${personId}`, 200, {
    person_id: personId,
    revision: 4,
    display_name: 'Alex Rivera',
    work_email: 'alex@acme.test',
    source_kind: 'manual',
    last_changed_by: actor,
    last_changed_at: '2026-09-20T00:00:00Z',
  });
  api.reply(`/api/v1/tenants/${tenantId}/members`, 200, {
    items: [],
    next_cursor: null,
  });
  api.reply(base, 200, { items: [], next_cursor: null });
  api.reply(`POST ${base}`, 200, { observation_id: observationId });
  const container = mount(() => PersonDetailPage({ personId }));
  await vi.waitFor(() =>
    expect(
      container.querySelector('form.workforce-source-record')
    ).not.toBeNull()
  );
  const form = container.querySelector('form.workforce-source-record')!;

  setField(form, 'Source system', 'Payroll');
  setField(form, 'Source record ID', 'W-100');
  setField(form, 'Source revision', 'export-7');
  setField(form, 'Observed at', '2026-09-20T12:00');
  setField(form, 'Observed display name', 'Alex R. Rivera');
  form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

  await vi.waitFor(() =>
    expect(container.textContent).toContain('Source observation recorded')
  );
  expect(api.bodies.find((item) => item.method === 'POST')?.body).toEqual({
    source: {
      source_kind: 'hris',
      source_system: 'Payroll',
      source_record_id: 'W-100',
      source_revision: 'export-7',
    },
    target_kind: 'person',
    target_id: personId,
    expected_target_revision: 4,
    observed_at: new Date('2026-09-20T12:00').toISOString(),
    facts: {
      person: { display_name: 'Alex R. Rivera', work_email: 'alex@acme.test' },
    },
  });
  expect(api.bodies.filter((item) => item.method === 'PUT')).toHaveLength(0);
  expect(
    container.querySelector(
      `a[href="/acme/workforce/sources/${observationId}"]`
    )
  ).not.toBeNull();
});

function canonicalRelationship() {
  return {
    relationship_id: relationshipId,
    revision: 3,
    person_id: personId,
    source_worker_id: 'W-100',
    worker_type: 'employee',
    lifecycle_status: 'active',
    start_date: '2026-01-05',
    end_date: null,
    department: 'Engineering',
    manager_person_id: null,
    sponsor_person_id: null,
    employment_status_reason: 'Returned from leave',
    restricted_fields_redacted: false,
    source_kind: 'manual',
    last_changed_by: actor,
    last_changed_at: '2026-09-20T00:00:00Z',
  };
}

function relationshipReplies() {
  api.reply(
    `/api/v1/tenants/${tenantId}/work-relationships/${relationshipId}`,
    200,
    canonicalRelationship()
  );
  api.reply(`/api/v1/tenants/${tenantId}/people`, 200, {
    items: [],
    next_cursor: null,
  });
  api.reply(base, 200, { items: [], next_cursor: null });
}

function fillSourceIdentity(form: Element) {
  setField(form, 'Source system', 'Directory');
  setField(form, 'Source record ID', 'record-100');
  setField(form, 'Source revision', 'export-8');
  setField(form, 'Observed at', '2026-09-20T12:00');
}

it('ShouldRecordAllObservedRelationshipFactsWithoutChangingCanonicalTerms', async () => {
  relationshipReplies();
  api.reply(`POST ${base}`, 200, { observation_id: observationId });
  const container = mount(() => WorkRelationshipDetailPage({ relationshipId }));
  await vi.waitFor(() =>
    expect(
      container.querySelector('form.workforce-source-record')
    ).not.toBeNull()
  );
  const form = container.querySelector('form.workforce-source-record')!;
  fillSourceIdentity(form);
  setField(form, 'Source kind', 'idp');
  setField(form, 'Observed department', 'Platform');
  setField(form, 'Observed employment status reason', 'Source says active');
  form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

  await vi.waitFor(() =>
    expect(container.textContent).toContain('Source observation recorded')
  );
  expect(api.bodies.find((item) => item.method === 'POST')?.body).toMatchObject(
    {
      source: { source_kind: 'idp' },
      target_kind: 'work_relationship',
      target_id: relationshipId,
      expected_target_revision: 3,
      facts: {
        work_relationship: {
          worker_type: 'employee',
          lifecycle_status: 'active',
          start_date: '2026-01-05',
          end_date: null,
          department: 'Platform',
          manager_person_id: null,
          sponsor_person_id: null,
          employment_status_reason: 'Source says active',
        },
      },
    }
  );
  expect(api.bodies.filter((item) => item.method === 'PUT')).toHaveLength(0);
});

it('ShouldAllowExplicitCanonicalEmploymentReasonCorrection', async () => {
  relationshipReplies();
  const path = `/api/v1/tenants/${tenantId}/work-relationships/${relationshipId}`;
  api.reply(`PUT ${path}`, 204);
  const container = mount(() => WorkRelationshipDetailPage({ relationshipId }));
  await vi.waitFor(() =>
    expect(container.textContent).toContain('Revise terms')
  );
  const form = container.querySelector('form')!;
  setField(form, 'Employment status reason', 'Source verified active');
  form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

  await vi.waitFor(() =>
    expect(api.bodies.some((item) => item.method === 'PUT')).toBe(true)
  );
  expect(api.bodies.find((item) => item.method === 'PUT')?.body).toMatchObject({
    expected_revision: 3,
    employment_status_reason: 'Source verified active',
  });
});

it('ShouldCorroborateProviderExpiryWithoutSubmittingGovernedOwnerOrPurpose', async () => {
  api.reply(
    `/api/v1/tenants/${tenantId}/service-identities/${identityId}`,
    200,
    {
      service_identity_id: identityId,
      revision: 2,
      display_name: 'deploy-bot',
      identity_kind: 'bot',
      environment: 'production',
      purpose: 'Deploy the app',
      lifecycle_status: 'active',
      owner_kind: 'person',
      owner_id: personId,
      review_by: '2027-03-01',
      expires_on: '2026-11-01',
      expired: false,
      source_kind: 'manual',
      unowned: false,
      unowned_reasons: [],
      last_changed_by: actor,
      last_changed_at: '2026-09-20T00:00:00Z',
    }
  );
  api.reply(`/api/v1/tenants/${tenantId}/people`, 200, {
    items: [],
    next_cursor: null,
  });
  api.reply(`/api/v1/tenants/${tenantId}/teams`, 200, {
    items: [],
    next_cursor: null,
  });
  api.reply(base, 200, { items: [], next_cursor: null });
  api.reply(`POST ${base}`, 200, { observation_id: observationId });
  const container = mount(() =>
    ServiceIdentityDetailPage({ serviceIdentityId: identityId })
  );
  await vi.waitFor(() =>
    expect(
      container.querySelector('form.workforce-source-record')
    ).not.toBeNull()
  );
  const form = container.querySelector('form.workforce-source-record')!;
  fillSourceIdentity(form);
  setField(form, 'Observed credential expiry', '2026-12-01');
  form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

  await vi.waitFor(() =>
    expect(container.textContent).toContain('Source observation recorded')
  );
  expect(api.bodies.find((item) => item.method === 'POST')?.body).toMatchObject(
    {
      source: { source_kind: 'provider' },
      target_kind: 'service_identity',
      target_id: identityId,
      expected_target_revision: 2,
      facts: {
        service_identity: {
          display_name: 'deploy-bot',
          identity_kind: 'bot',
          environment: 'production',
          lifecycle_status: 'active',
          expires_on: '2026-12-01',
        },
      },
    }
  );
  expect(
    JSON.stringify(api.bodies.find((item) => item.method === 'POST')?.body)
  ).not.toMatch(/owner|purpose|review_by/);
  expect(form.querySelector('select')?.querySelectorAll('option')).toHaveLength(
    1
  );
});

it.each([
  {
    manager: personId,
    reason: null,
    disclosed: personId,
    hidden: 'private status reason',
  },
  {
    manager: null,
    reason: 'Returned from protected leave',
    disclosed: 'Returned from protected leave',
    hidden: personId,
  },
])(
  'ShouldShowOnlyDisclosedRelationshipFactsWhenPreviewPermissionIsDenied $disclosed',
  async ({ manager, reason, disclosed, hidden }) => {
    api.reply(
      `${base}/${observationId}`,
      200,
      observation({
        target_kind: 'work_relationship',
        target_id: relationshipId,
        restricted_fields_redacted: true,
        facts: {
          work_relationship: {
            worker_type: 'employee',
            lifecycle_status: 'active',
            start_date: '2026-01-05',
            end_date: null,
            department: 'Engineering',
            manager_person_id: manager,
            sponsor_person_id: null,
            employment_status_reason: reason,
          },
        },
      })
    );
    api.reply(`${base}/${observationId}/preview`, 403, {
      detail: 'Forbidden.',
    });
    const container = mount(() => WorkforceSourceDetailPage({ observationId }));

    await vi.waitFor(() =>
      expect(container.textContent).toContain(
        'You do not have permission to compare'
      )
    );
    expect(container.textContent).toContain(disclosed);
    expect(container.textContent).not.toContain(hidden);
    expect(container.textContent).toContain('No value disclosed');
    expect(container.textContent).toContain('Accepted for revision 4');
    expect(
      container.querySelector(
        `a[href="/acme/workforce/relationships/${relationshipId}"]`
      )?.textContent
    ).toContain('Canonical work relationship');
    expect(container.querySelector('textarea')).toBeNull();
    expect(button(container, 'Accept matching source')).toBeUndefined();
    expect(api.bodies.filter((item) => item.method !== 'GET')).toHaveLength(0);
  }
);

it('ShouldPreserveImmutableSourceContentForAnExplicitRecordRetry', async () => {
  api.reply(base, 200, { items: [], next_cursor: null });
  api.reply(`POST ${base}`, 409, {
    detail: 'This source identity has different recorded content.',
  });
  const container = mount(() =>
    SourceObservationsPanel({
      target: {
        kind: 'person',
        id: personId,
        revision: 4,
        facts: { person: { display_name: 'Alex Rivera', work_email: null } },
      },
    })
  );
  const form = container.querySelector('form')!;
  fillSourceIdentity(form);
  form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

  await vi.waitFor(() =>
    expect(container.textContent).toContain(
      'Keep the original content for a retry'
    )
  );
  expect(api.bodies.filter((item) => item.method === 'POST')).toHaveLength(1);
  api.reply(`POST ${base}`, 200, { observation_id: observationId });
  form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
  await vi.waitFor(() =>
    expect(container.textContent).toContain('Source observation recorded')
  );
  const records = api.bodies.filter((item) => item.method === 'POST');
  expect(records[1]?.body).toEqual(records[0]?.body);
});

it('ShouldRequestEverySourcePageWithTheExplicitCanonicalCorrelation', async () => {
  const queries: URLSearchParams[] = [];
  const fetch = globalThis.fetch;
  vi.stubGlobal('fetch', (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(input instanceof Request ? input.url : String(input));
    if (url.pathname === base) {
      queries.push(url.searchParams);
      api.reply(
        base,
        200,
        url.searchParams.has('cursor')
          ? {
              items: [observation({ observation_id: 'second' })],
              next_cursor: null,
            }
          : { items: [observation()], next_cursor: 'page-2' }
      );
    }
    return fetch(input, init);
  });

  const result = await listSourceObservations({ kind: 'person', id: personId });

  expect(result.map((item) => item.observation_id)).toEqual([
    observationId,
    'second',
  ]);
  expect(
    queries.map((query) => [
      query.get('target_kind'),
      query.get('target_id'),
      query.get('limit'),
    ])
  ).toEqual([
    ['person', personId, '200'],
    ['person', personId, '200'],
  ]);
  expect(queries[1]?.get('cursor')).toBe('page-2');
});

it('ShouldShowGenericPermissionFailureGivenRelationshipSourceRecordingIsRevoked', async () => {
  relationshipReplies();
  api.reply(`POST ${base}`, 403, {
    detail: 'Source comparison requires both workforce field read grants.',
  });
  const container = mount(() => WorkRelationshipDetailPage({ relationshipId }));
  await vi.waitFor(() =>
    expect(
      container.querySelector('form.workforce-source-record')
    ).not.toBeNull()
  );
  const form = container.querySelector('form.workforce-source-record')!;
  fillSourceIdentity(form);
  form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
  await vi.waitFor(() =>
    expect(container.querySelector('[role="alert"]')?.textContent).toBe(
      'You do not have permission to record this source observation.'
    )
  );
  expect(container.textContent).not.toContain(
    'Keep the original content for a retry'
  );
  expect(container.textContent).not.toContain('Source observation recorded');
  expect(api.bodies.filter((item) => item.method === 'POST')).toHaveLength(1);
});

it('ShouldPreserveInitialAttributionGivenAnImmutableDecidedObservation', async () => {
  const decision = {
    outcome: 'dismissed',
    note: 'Kept canonical spelling',
    target_revision: 4,
    actor: { display: 'Original Lead' },
    decided_at: '2026-09-22T00:00:00Z',
  };
  api.reply(`${base}/${observationId}`, 200, observation({ decision }));
  api.reply(`${base}/${observationId}/preview`, 200, preview({ decision }));
  const container = mount(() => WorkforceSourceDetailPage({ observationId }));

  await vi.waitFor(() =>
    expect(container.textContent).toContain(
      'Kept canonical spelling — Original Lead'
    )
  );
  expect(container.textContent).toContain(
    'Dismissed; canonical revision 4 retained'
  );
  expect(container.querySelector('textarea')).toBeNull();
  expect(button(container, 'Dismiss and keep canonical')).toBeUndefined();
  expect(api.bodies.filter((item) => item.method !== 'GET')).toHaveLength(0);
});

it('ShouldMarkAcceptedProvenanceHistoricalGivenAnAdvancedCanonicalRevision', async () => {
  api.reply(
    `${base}/${observationId}`,
    200,
    observation({
      current_target_revision: 6,
      accepted_for_current_revision: false,
    })
  );
  api.reply(
    `${base}/${observationId}/preview`,
    200,
    preview({ current_target_revision: 6, decision: observation().decision })
  );
  const container = mount(() => WorkforceSourceDetailPage({ observationId }));
  await vi.waitFor(() =>
    expect(container.textContent).toContain(
      'Accepted for historical revision 4; current revision is 6'
    )
  );
  expect(container.textContent).not.toContain(
    'Accepted for the current revision'
  );
  expect(container.querySelector('textarea')).toBeNull();
  expect(await accessibilityViolations(container)).toEqual([]);
});

it('ShouldRequireARefreshedComparisonAfterAStaleDecisionWithoutRetryingAutomatically', async () => {
  api.reply(
    `${base}/${observationId}`,
    200,
    observation({ revision: 1, decision: null })
  );
  api.reply(
    `${base}/${observationId}/preview`,
    200,
    preview({ conflicting_fields: [], can_accept: true })
  );
  api.reply(`PUT ${base}/${observationId}/decision`, 409, {
    detail: 'The canonical revision changed.',
  });
  const container = mount(() => WorkforceSourceDetailPage({ observationId }));
  await vi.waitFor(() =>
    expect(container.textContent).toContain('Observed facts match')
  );
  setField(container, 'Decision note', 'Checked current roster');
  await vi.waitFor(() =>
    expect(button(container, 'Accept matching source').disabled).toBe(false)
  );
  button(container, 'Accept matching source').click();

  await vi.waitFor(() =>
    expect(container.textContent).toContain(
      'Refresh comparison before deciding again'
    )
  );
  expect(button(container, 'Accept matching source').disabled).toBe(true);
  expect(button(container, 'Dismiss and keep canonical').disabled).toBe(true);
  api.reply(
    `${base}/${observationId}/preview`,
    200,
    preview({ current_target_revision: 6 })
  );
  button(container, 'Refresh comparison').click();
  await vi.waitFor(() =>
    expect(container.textContent).toContain('Canonical revision 6')
  );
  expect(button(container, 'Accept matching source').disabled).toBe(true);
  expect(button(container, 'Dismiss and keep canonical').disabled).toBe(false);
  expect(api.bodies.filter((item) => item.method === 'PUT')).toHaveLength(1);
});

it('ShouldRetryALaggingPreviewWithoutCallingItAConflict', async () => {
  api.reply(
    `${base}/${observationId}`,
    200,
    observation({ revision: 1, decision: null })
  );
  api.reply(`${base}/${observationId}/preview`, 409, {
    detail: 'Projection behind',
    transient: true,
  });
  const container = mount(() => WorkforceSourceDetailPage({ observationId }));
  await vi.waitFor(() =>
    expect(container.textContent).toContain('still being processed')
  );
  expect(container.textContent).not.toContain('Conflicting fields');
  expect(button(container, 'Accept matching source')).toBeUndefined();
  api.reply(
    `${base}/${observationId}/preview`,
    200,
    preview({ conflicting_fields: [], can_accept: true })
  );
  button(container, 'Try again').click();
  await vi.waitFor(() =>
    expect(container.textContent).toContain('Observed facts match')
  );
  expect(api.bodies.filter((item) => item.method !== 'GET')).toHaveLength(0);
});

it('ShouldKeepRestrictedFactsOutOfSourceListsEvenWhenTheServerProvidesThem', async () => {
  api.reply(base, 200, {
    items: [
      observation({
        target_kind: 'work_relationship',
        target_id: relationshipId,
        facts: {
          work_relationship: {
            manager_person_id: personId,
            employment_status_reason: 'PRIVATE REASON',
          },
        },
      }),
    ],
    next_cursor: null,
  });
  const container = mount(WorkforceSourcesPage);
  await vi.waitFor(() => expect(container.textContent).toContain('Payroll'));
  expect(container.textContent).not.toContain(personId);
  expect(container.textContent).not.toContain('PRIVATE REASON');
  expect(await accessibilityViolations(container)).toEqual([]);
});

it.each([
  { manager_person_id: personId, employment_status_reason: null },
  {
    manager_person_id: null,
    employment_status_reason: 'Returned from protected leave',
  },
])(
  'ShouldAvoidReplacingUnknownPrivateTermsGivenRedactedRelationshipDetails %#',
  async (disclosed) => {
    relationshipReplies();
    api.reply(
      `/api/v1/tenants/${tenantId}/work-relationships/${relationshipId}`,
      200,
      {
        ...canonicalRelationship(),
        ...disclosed,
        restricted_fields_redacted: true,
      }
    );
    const container = mount(() =>
      WorkRelationshipDetailPage({ relationshipId })
    );

    await vi.waitFor(() =>
      expect(container.textContent).toContain(
        'Restricted details must be available before editing'
      )
    );
    expect(container.querySelector('form')).toBeNull();
    expect(container.textContent).not.toContain('No manager');
    expect(container.textContent).toContain(
      'Source recording needs unrestricted relationship details'
    );
    if (disclosed.employment_status_reason)
      expect(container.textContent).toContain(
        disclosed.employment_status_reason
      );
    expect(api.bodies.filter((item) => item.method !== 'GET')).toHaveLength(0);
  }
);
