// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { BoundariesPage } from './pages/boundaries-list.js';
import { BoundaryDetailPage } from './pages/boundary-detail.js';
import { BoundaryExceptionsPage } from './pages/boundary-exceptions.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const programId = '0190a1b2-0000-7000-8000-0000000000b1';
const boundaryId = '0190a1b2-0000-7000-8000-0000000000d1';
const draftId = '0190a1b2-0000-7000-8000-0000000000d2';
const approvedId = '0190a1b2-0000-7000-8000-0000000000d3';
const reviewId = '0190a1b2-0000-7000-8000-0000000000e1';
const waiverId = '0190a1b2-0000-7000-8000-0000000000f1';
const memberUser = '0190a1b2-0000-7000-8000-0000000000a1';
const memberId = '0190a1b2-0000-7000-8000-0000000000a9';
const base = `/api/v1/tenants/${tenantId}`;
const list = `${base}/programs/${programId}/boundaries`;
const detail = `${base}/boundaries/${boundaryId}`;
const draftPath = `${detail}/drafts/${draftId}`;
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

const content = {
  statement: 'The Acme payments platform and its supporting services.',
  engagement_stage: 'type_i',
  trust_services_categories: ['security', 'availability'],
  entries: [
    {
      entry_id: '0190a1b2-0000-7000-8000-0000000000c1',
      kind: 'inclusion',
      subject_type: 'application',
      subject: 'Payments API',
      governed_record_id: '0190a1b2-0000-7000-8000-0000000000c9',
      owner_reference: 'Platform lead',
      rationale: 'Processes customer payments.',
      unresolved: false,
    },
    {
      entry_id: '0190a1b2-0000-7000-8000-0000000000c2',
      kind: 'exclusion',
      subject_type: 'provider',
      subject: 'Card network',
      governed_record_id: null,
      owner_reference: 'Finance',
      rationale: 'Carved out subservice organization.',
      unresolved: true,
    },
  ],
};

function version(id: string, revision: number, status: string, effective: string | null = null) {
  return {
    tenant_id: tenantId,
    boundary_id: boundaryId,
    program_id: programId,
    version_id: id,
    revision,
    content,
    status,
    effective_from: effective,
    author_member_id: 'author',
    author_display: 'Avery Author',
    changed_at: '2026-09-20T00:00:00Z',
  };
}

function decision(outcome: string, revision = 3, waiver: string | null = null) {
  return {
    tenant_id: tenantId,
    boundary_id: boundaryId,
    decision_id: reviewId,
    version_id: draftId,
    revision,
    outcome,
    actor_member_id: 'reviewer',
    actor_display: 'Riley Reviewer',
    rationale: 'Scope matches the system description.',
    decided_at: '2026-09-21T00:00:00Z',
    supersedes_decision_id: null,
    relies_on_decision_id: null,
    impact_digest: null,
    separation_of_duties_waiver_id: waiver,
  };
}

function boundaryBody(withDraft = true) {
  return {
    tenant_id: tenantId,
    boundary_id: boundaryId,
    program_id: programId,
    draft: withDraft ? version(draftId, 3, 'draft') : null,
    latest_approved_version: version(approvedId, 1, 'approved', '2026-09-01'),
    latest_decision: null,
    revision: 5,
  };
}

function assignment(type: string, id: string) {
  return {
    tenant_id: tenantId,
    assignment_id: id,
    member_id: memberId,
    type,
    scope: { record_type: 'boundary', record_id: boundaryId, version_id: draftId, revision: 3 },
    assigned_at: '2026-09-20T00:00:00Z',
    assigned_by_member_id: 'admin',
    effective_from: '2026-09-20T00:00:00Z',
    effective_until: null,
    revoked_at: null,
    revoked_by_member_id: '00000000-0000-0000-0000-000000000000',
    separation_of_duties_waiver_ids: [],
    assigned_by_display: 'Adrian Admin',
  };
}

function preview(digest = 'digest-1', complete = true) {
  return {
    tenant_id: tenantId,
    boundary_id: boundaryId,
    draft_version_id: draftId,
    revision: 3,
    approved_version_id: approvedId,
    changes: [
      {
        field: 'entries',
        change_type: 'added',
        entry_id: content.entries[1].entry_id,
        previous_value: null,
        proposed_value: null,
        previous_entry: null,
        proposed_entry: content.entries[1],
      },
    ],
    contributions: [
      {
        context: 'controls',
        records: [{ tenant_id: tenantId, context: 'controls', record_type: 'control', record_id: 'c', reason: 'Applies to the Payments API.' }],
        complete: true,
      },
    ],
    pending_contexts: complete ? [] : ['risks'],
    complete,
    digest,
  };
}

function waiverBody(approved = false) {
  return {
    tenant_id: tenantId,
    waiver_id: waiverId,
    scope: { record_type: 'boundary', record_id: boundaryId, version_id: draftId, revision: 3, action: 'review' },
    beneficiary_member_id: memberId,
    requester_member_id: 'admin',
    requester_display: 'Adrian Admin',
    rationale: 'Only one qualified reviewer this week.',
    requested_at: '2026-09-22T00:00:00Z',
    expires_at: '2099-09-29T23:59:59Z',
    approver_member_id: approved ? 'second' : null,
    approver_display: approved ? 'Sam Second' : null,
    approved_at: approved ? '2026-09-22T01:00:00Z' : null,
    status: approved ? 'approved' : 'pending_approval',
    active: approved,
  };
}

function detailAnswers(options: { decisions?: unknown[]; assignments?: unknown[]; withDraft?: boolean } = {}) {
  api.reply(`GET ${detail}`, 200, boundaryBody(options.withDraft ?? true));
  api.reply(`GET ${detail}/versions`, 200, {
    items: [version(approvedId, 1, 'approved', '2026-09-01'), version(draftId, 3, 'draft')],
    next_cursor: null,
  });
  api.reply(`GET ${detail}/decisions`, 200, { items: options.decisions ?? [], next_cursor: null });
  api.reply(`GET ${detail}/effective-version`, 200, version(approvedId, 1, 'approved', '2026-09-01'));
  api.reply(`GET ${base}/members`, 200, {
    items: [{ user_id: memberUser, tenant_id: tenantId, verified_email_address: 'riley@acme.test' }],
    next_cursor: null,
  });
  api.reply(`GET ${base}/responsibilities`, 200, {
    tenant_id: tenantId,
    set_id: 's',
    scope: { record_type: 'boundary', record_id: boundaryId, version_id: draftId, revision: 3 },
    revision: 1,
    assignments: options.assignments ?? [],
  });
  api.reply(`GET ${draftPath}/impact-preview`, 200, preview());
}

function field(container: HTMLElement, label: string) {
  return [...container.querySelectorAll('label')].find((l) => l.textContent?.trim().startsWith(label))!
    .querySelector('input, textarea, select') as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement;
}

function type(element: HTMLInputElement | HTMLTextAreaElement, value: string) {
  element.value = value;
  element.dispatchEvent(new Event('input', { bubbles: true }));
}

function choose(element: HTMLSelectElement, value: string) {
  element.value = value;
  element.dispatchEvent(new Event('change', { bubbles: true }));
}

function check(element: HTMLInputElement) {
  element.checked = true;
  element.dispatchEvent(new Event('change', { bubbles: true }));
}

function button(container: HTMLElement, text: string) {
  return [...container.querySelectorAll('button')].find((b) => b.textContent?.trim() === text) as HTMLButtonElement | undefined;
}

function submit(form: Element) {
  form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
}

beforeEach(async () => {
  clearActiveTenant();
  api = stubApi();
  api.reply('/api/v1/tenant-slugs/acme/mine', 200, { tenant_id: tenantId, current_slug: 'acme', redirect: false });
  api.reply('/api/v1/tenants/mine', 200, { items: [{ tenant_id: tenantId, name: 'Acme Corp', slug: 'acme' }], next_cursor: null });
  await resolveTenantRoute('acme', { pathname: '/acme/programs', search: '', hash: '' });
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('boundary authoring (R1-02 frontend #178)', () => {
  it('ShouldListBoundariesWithLinks', async () => {
    // Arrange
    api.reply(`GET ${list}`, 200, { items: [boundaryBody()], next_cursor: null });

    // Act
    const container = mount(() => <BoundariesPage programId={programId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('draft revision 3'));

    // Assert
    expect(container.querySelector(`a[href="/acme/programs/${programId}/boundaries/${boundaryId}"]`)).not.toBeNull();
    expect(container.textContent).toContain('approved revision 1');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldCreateABoundaryWithSecurityAndAnUnresolvedEntry', async () => {
    // Arrange
    api.reply(`GET ${list}`, 200, { items: [], next_cursor: null });
    api.reply(`POST ${list}`, 400, problem(400, 'The boundary requires a statement.'));
    const container = mount(() => <BoundariesPage programId={programId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('No boundaries yet'));
    type(field(container, 'System description statement') as HTMLTextAreaElement, 'Payments platform');
    button(container, 'Add scope entry')!.click();
    await vi.waitFor(() => expect(container.querySelector('.boundary-entry')).not.toBeNull());
    type(field(container, 'Subject') as HTMLInputElement, 'Payments API');
    type(field(container, 'Owner') as HTMLInputElement, 'Platform lead');
    type(field(container, 'Rationale') as HTMLTextAreaElement, 'Core service');

    // Act
    submit(container.querySelector('form')!);
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    const sent = api.bodies.find((b) => b.method === 'POST' && b.path === list)?.body as {
      content: { statement: string; trust_services_categories: string[]; entries: { unresolved: boolean; governed_record_id: null }[] };
    };
    expect(sent.content.statement).toBe('Payments platform');
    expect(sent.content.trust_services_categories).toEqual(['security']);
    expect(sent.content.entries[0].unresolved).toBe(true);
    expect(sent.content.entries[0].governed_record_id).toBeNull();
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('requires a statement');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldShowForbiddenGivenTheViewerCannotReadBoundaries', async () => {
    // Arrange
    api.reply(`GET ${list}`, 403, problem(403, 'Forbidden'));

    // Act
    const container = mount(() => <BoundariesPage programId={programId} />);

    // Assert
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Boundaries are not available to you'));
    expect(container.querySelector('form')).toBeNull();
  });

  it('ShouldDistinguishGovernedLinksFromUnresolvedReferences', async () => {
    // Arrange
    detailAnswers({ decisions: [decision('accept')] });

    // Act
    const container = mount(() => <BoundaryDetailPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(container.querySelectorAll('.boundary-decisions li')).toHaveLength(1));

    // Assert
    expect(container.textContent).toContain('Governed inventory link');
    expect(container.textContent).toContain('Unresolved reference');
    expect(container.textContent).toContain('Draft revision 3 by Avery Author');
    expect(container.querySelector('.boundary-decisions li')?.textContent).toContain('Riley Reviewer');
    expect(api.requested.filter((path) => path.startsWith('/api/v1/tenants/') && path !== '/api/v1/tenants/mine').every((path) => path.startsWith(base))).toBe(true);
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldReviseTheDraftAgainstTheExpectedRevision', async () => {
    // Arrange
    detailAnswers();
    api.reply(`PUT ${draftPath}`, 204);
    const container = mount(() => <BoundaryDetailPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(container.querySelector('.boundary-form')).not.toBeNull());
    type(field(container, 'System description statement') as HTMLTextAreaElement, 'Revised statement');

    // Act
    submit(container.querySelector('.boundary-form')!);
    await vi.waitFor(() => expect(api.bodies.some((b) => b.method === 'PUT')).toBe(true));

    // Assert
    const sent = api.bodies.find((b) => b.method === 'PUT' && b.path === draftPath)?.body as {
      expected_revision: number;
      content: { statement: string; entries: unknown[] };
    };
    expect(sent.expected_revision).toBe(3);
    expect(sent.content.statement).toBe('Revised statement');
    expect(sent.content.entries).toHaveLength(2);
  });

  it('ShouldAskToReloadGivenAStaleDraftRevision', async () => {
    // Arrange
    detailAnswers();
    api.reply(`PUT ${draftPath}`, 409, problem(409, 'The boundary draft revision is stale.'));
    const container = mount(() => <BoundaryDetailPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(container.querySelector('.boundary-form')).not.toBeNull());

    // Act
    submit(container.querySelector('.boundary-form')!);
    await vi.waitFor(() => expect(container.querySelector('.boundary-form [role="alert"]')).not.toBeNull());

    // Assert
    expect(container.querySelector('.boundary-form [role="alert"]')?.textContent).toContain('Someone else changed this boundary');
  });

  it('ShouldExplainProjectionLagGivenATransientConflict', async () => {
    // Arrange
    api.reply(`GET ${detail}`, 409, problem(409, 'Projection is behind.', true));

    // Act
    const container = mount(() => <BoundaryDetailPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(container.querySelector('h1')).not.toBeNull());

    // Assert
    expect(container.textContent).toContain('still being processed');
    expect(button(container, 'Try again')).not.toBeUndefined();
  });

  it('ShouldShowNotFoundGivenABoundaryFromAnotherOrganization', async () => {
    // Arrange
    api.reply(`GET ${detail}`, 404, problem(404, 'The boundary was not found.'));

    // Act
    const container = mount(() => <BoundaryDetailPage programId={programId} boundaryId={boundaryId} />);

    // Assert
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Boundary not found'));
  });

  it('ShouldShowTheSelectedVersion', async () => {
    // Arrange
    detailAnswers();
    const container = mount(() => <BoundaryDetailPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(button(container, 'Revision 1 · approved')).not.toBeUndefined());

    // Act
    button(container, 'Revision 1 · approved')!.click();

    // Assert
    await vi.waitFor(() => expect(container.querySelector('section[aria-label="Revision 1"]')).not.toBeNull());
    expect(container.querySelector('section[aria-label="Revision 1"]')?.textContent).toContain('Card network');
  });

  it('ShouldProposeASuccessorFromTheApprovedVersion', async () => {
    // Arrange
    detailAnswers({ withDraft: false });
    api.reply(`POST ${detail}/successors`, 200, { boundary_id: boundaryId, draft_version_id: draftId });
    const container = mount(() => <BoundaryDetailPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(button(container, 'Propose successor draft')).not.toBeUndefined());

    // Act
    submit(container.querySelector('.boundary-form')!);
    await vi.waitFor(() => expect(api.bodies.some((b) => b.path === `${detail}/successors`)).toBe(true));

    // Assert
    const sent = api.bodies.find((b) => b.path === `${detail}/successors`)?.body as { expected_approved_version_id: string };
    expect(sent.expected_approved_version_id).toBe(approvedId);
  });
});

describe('boundary review, approval, and impact (R1-02 frontend #178)', () => {
  it('ShouldShowImpactAndRequireAnAcceptedReviewBeforeApproval', async () => {
    // Arrange
    detailAnswers();

    // Act
    const container = mount(() => <BoundaryDetailPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Applies to the Payments API.'));

    // Assert
    expect(container.querySelector('.boundary-changes')?.textContent).toContain('Card network');
    expect(container.textContent).toContain('Approval needs an accepted review of this revision first.');
    expect(button(container, 'Approve boundary')?.disabled).toBe(true);
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldRecordAReviewOverHttp', async () => {
    // Arrange
    detailAnswers();
    api.reply(`POST ${draftPath}/reviews`, 204);
    const container = mount(() => <BoundaryDetailPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(container.querySelector('.boundary-review')).not.toBeNull());
    type(field(container, 'Review rationale') as HTMLTextAreaElement, 'Matches the description.');

    // Act
    submit(container.querySelector('.boundary-review')!);
    await vi.waitFor(() => expect(api.bodies.some((b) => b.path === `${draftPath}/reviews`)).toBe(true));

    // Assert
    const sent = api.bodies.find((b) => b.path === `${draftPath}/reviews`)?.body as Record<string, unknown>;
    expect(sent).toEqual({ expected_revision: 3, outcome: 'accept', rationale: 'Matches the description.' });
  });

  it('ShouldShowTheDenialGivenTheAuthorReviewsTheirOwnDraft', async () => {
    // Arrange
    detailAnswers();
    api.reply(`POST ${draftPath}/reviews`, 403, problem(403, 'A boundary author cannot review their own draft.'));
    const container = mount(() => <BoundaryDetailPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(container.querySelector('.boundary-review')).not.toBeNull());
    type(field(container, 'Review rationale') as HTMLTextAreaElement, 'Self review');

    // Act
    submit(container.querySelector('.boundary-review')!);

    // Assert
    await vi.waitFor(() => expect(container.textContent).toContain('This decision was denied'));
  });

  it('ShouldApproveWithTheAcknowledgedDigestAndAcceptedReview', async () => {
    // Arrange
    detailAnswers({ decisions: [decision('accept')] });
    api.reply(`POST ${draftPath}/approvals`, 204);
    const container = mount(() => <BoundaryDetailPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Cites the accepted review by Riley Reviewer'));
    await vi.waitFor(() => expect(container.textContent).toContain('Applies to the Payments API.'));
    check(field(container, 'I reviewed the current impact preview') as HTMLInputElement);
    type(field(container, 'Approval rationale') as HTMLTextAreaElement, 'Ready for Type I.');
    await vi.waitFor(() => expect(button(container, 'Approve boundary')?.disabled).toBe(false));

    // Act
    submit(container.querySelector('.boundary-approve')!);
    await vi.waitFor(() => expect(api.bodies.some((b) => b.path === `${draftPath}/approvals`)).toBe(true));

    // Assert
    const sent = api.bodies.find((b) => b.path === `${draftPath}/approvals`)?.body as Record<string, unknown>;
    expect(sent.impact_digest).toBe('digest-1');
    expect(sent.accepted_review_decision_id).toBe(reviewId);
    expect(sent.expected_revision).toBe(3);
    expect(sent.separation_of_duties_waiver_id).toBeUndefined();
  });

  it('ShouldRequireAFreshPreviewGivenTheImpactChangedBeforeApproval', async () => {
    // Arrange
    detailAnswers({ decisions: [decision('accept')] });
    api.reply(`POST ${draftPath}/approvals`, 409, problem(409, 'The impact preview changed. Reload it before approval.'));
    const container = mount(() => <BoundaryDetailPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Applies to the Payments API.'));
    check(field(container, 'I reviewed the current impact preview') as HTMLInputElement);
    type(field(container, 'Approval rationale') as HTMLTextAreaElement, 'Ready.');
    await vi.waitFor(() => expect(button(container, 'Approve boundary')?.disabled).toBe(false));
    const previewsBefore = api.requested.filter((path) => path.endsWith('/impact-preview')).length;

    // Act
    submit(container.querySelector('.boundary-approve')!);

    // Assert
    await vi.waitFor(() => expect(container.textContent).toContain('acknowledge it again'));
    await vi.waitFor(() =>
      expect(api.requested.filter((path) => path.endsWith('/impact-preview')).length).toBeGreaterThan(previewsBefore)
    );
    expect(button(container, 'Approve boundary')?.disabled).toBe(true);
  });

  it('ShouldBlockApprovalGivenAnIncompleteImpactPreview', async () => {
    // Arrange
    detailAnswers({ decisions: [decision('accept')] });
    api.reply(`GET ${draftPath}/impact-preview`, 200, preview('digest-2', false));

    // Act
    const container = mount(() => <BoundaryDetailPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('The impact preview is incomplete'));

    // Assert
    expect(container.textContent).toContain('waiting on risks');
    expect((field(container, 'I reviewed the current impact preview') as HTMLInputElement).disabled).toBe(true);
  });
});

describe('separation-of-duties conflicts and exceptions (R1-04e frontend #193)', () => {
  it('ShouldShowStandingConflictsBeforeReviewAndApproval', async () => {
    // Arrange
    detailAnswers({
      assignments: [assignment('evidence_contributor', 'a1'), assignment('assigned_reviewer', 'a2')],
    });

    // Act
    const container = mount(() => <BoundaryDetailPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(container.querySelector('.boundary-conflicts-summary li')).not.toBeNull());

    // Assert
    const summary = container.querySelector('.boundary-conflicts-summary')!;
    expect(summary.textContent).toContain('self review');
    expect(summary.compareDocumentPosition(container.querySelector('.boundary-review')!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(summary.querySelector(`a[href="/acme/programs/${programId}/boundaries/${boundaryId}/exceptions"]`)).not.toBeNull();
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldPreviewConflictsBeforeAssigning', async () => {
    // Arrange
    detailAnswers({ assignments: [assignment('evidence_contributor', 'a1')] });
    api.reply(`GET ${base}/responsibilities/conflict-preview`, 200, {
      scope: { record_type: 'boundary', record_id: boundaryId, version_id: draftId, revision: 3 },
      revision: 1,
      conflicts: [
        {
          kind: 'self_review',
          member_id: memberId,
          scope: { record_type: 'boundary', record_id: boundaryId, version_id: draftId, revision: 3 },
          existing_assignment_id: 'a1',
          proposed_assignment_id: 'p',
          existing_type: 'evidence_contributor',
          proposed_type: 'assigned_reviewer',
          waiver_action: 'review',
        },
      ],
    });
    api.reply(`POST ${base}/responsibilities`, 204);
    const container = mount(() => <BoundaryDetailPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(container.querySelector('.boundary-assign option[value="' + memberUser + '"]')).not.toBeNull());
    choose(field(container, 'Member') as HTMLSelectElement, memberUser);

    // Act
    submit(container.querySelector('.boundary-assign')!);
    await vi.waitFor(() => expect(container.querySelector('.boundary-assign .boundary-conflicts')).not.toBeNull());

    // Assert
    expect(api.bodies.some((b) => b.method === 'POST' && b.path === `${base}/responsibilities`)).toBe(false);
    expect(container.querySelector('.boundary-assign .boundary-conflicts')?.textContent).toContain('review exception');
    type(field(container, 'Approved exception IDs') as HTMLInputElement, waiverId);
    submit(container.querySelector('.boundary-assign')!);
    await vi.waitFor(() => expect(api.bodies.some((b) => b.method === 'POST' && b.path === `${base}/responsibilities`)).toBe(true));
    const sent = api.bodies.find((b) => b.method === 'POST' && b.path === `${base}/responsibilities`)?.body as Record<string, unknown>;
    expect(sent.member_user_id).toBe(memberUser);
    expect(sent.type).toBe('assigned_reviewer');
    expect(sent.record_type).toBe('boundary');
    expect(sent.scope_revision).toBe(3);
    expect(sent.separation_of_duties_waiver_ids).toEqual([waiverId]);
  });

  it('ShouldRecordAnExceptionWithActorRationaleScopeAndExpiry', async () => {
    // Arrange
    detailAnswers();
    api.reply(`POST ${base}/separation-of-duties-waivers`, 200, waiverBody());
    api.reply(`GET ${base}/separation-of-duties-waivers/${waiverId}`, 200, waiverBody());
    const container = mount(() => <BoundaryExceptionsPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(container.querySelector(`option[value="${memberUser}"]`)).not.toBeNull());
    choose(field(container, 'Member who needs the exception') as HTMLSelectElement, memberUser);
    type(field(container, 'Rationale') as HTMLTextAreaElement, 'Only one qualified reviewer this week.');
    type(field(container, 'Expires at the end of') as HTMLInputElement, '2099-09-29');

    // Act
    submit(container.querySelector('form')!);
    await vi.waitFor(() => expect(container.querySelector('.boundary-waiver')).not.toBeNull());

    // Assert
    const sent = api.bodies.find((b) => b.path === `${base}/separation-of-duties-waivers`)?.body as {
      scope: Record<string, unknown>;
      beneficiary_user_id: string;
      expires_at: string;
    };
    expect(sent.scope).toEqual({ record_type: 'boundary', record_id: boundaryId, version_id: draftId, revision: 3, action: 'review' });
    expect(sent.beneficiary_user_id).toBe(memberUser);
    expect(sent.expires_at).toBe('2099-09-29T23:59:59.000Z');
    const summary = container.querySelector('.boundary-waiver')!.textContent!;
    expect(summary).toContain('Adrian Admin');
    expect(summary).toContain('Only one qualified reviewer this week.');
    expect(summary).toContain('boundary revision 3');
    expect(summary).toContain('Awaiting approval by a different administrator');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldAttributeTheSecondAdministratorApproval', async () => {
    // Arrange
    detailAnswers();
    window.history.replaceState(null, '', `/acme/programs/${programId}/boundaries/${boundaryId}/exceptions?exception=${waiverId}`);
    api.reply(`GET ${base}/separation-of-duties-waivers/${waiverId}`, 200, waiverBody());
    api.reply(`POST ${base}/separation-of-duties-waivers/${waiverId}/approvals`, 200, waiverBody(true));
    const container = mount(() => <BoundaryExceptionsPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(button(container, 'Approve as second administrator')).not.toBeUndefined());
    api.reply(`GET ${base}/separation-of-duties-waivers/${waiverId}`, 200, waiverBody(true));

    // Act
    button(container, 'Approve as second administrator')!.click();

    // Assert
    await vi.waitFor(() => expect(container.querySelector('.boundary-waiver')?.textContent).toContain('Sam Second'));
    expect(container.querySelector('.boundary-waiver')?.textContent).toContain('active');
    window.history.replaceState(null, '', '/');
  });

  it('ShouldExplainTheDenialGivenTheRequesterApprovesTheirOwnException', async () => {
    // Arrange
    detailAnswers();
    window.history.replaceState(null, '', `/acme/programs/${programId}/boundaries/${boundaryId}/exceptions?exception=${waiverId}`);
    api.reply(`GET ${base}/separation-of-duties-waivers/${waiverId}`, 200, waiverBody());
    api.reply(`POST ${base}/separation-of-duties-waivers/${waiverId}/approvals`, 403, problem(403, 'A different administrator must approve.'));
    const container = mount(() => <BoundaryExceptionsPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(button(container, 'Approve as second administrator')).not.toBeUndefined());

    // Act
    button(container, 'Approve as second administrator')!.click();

    // Assert
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')?.textContent).toContain('do not have permission'));
    window.history.replaceState(null, '', '/');
  });

  it('ShouldLinkDecisionsMadeUnderAnException', async () => {
    // Arrange
    detailAnswers({ decisions: [decision('accept', 3, waiverId)] });

    // Act
    const container = mount(() => <BoundaryDetailPage programId={programId} boundaryId={boundaryId} />);
    await vi.waitFor(() => expect(container.querySelector('.boundary-decisions a')).not.toBeNull());

    // Assert
    expect(container.querySelector('.boundary-decisions a')?.getAttribute('href')).toBe(
      `/acme/programs/${programId}/boundaries/${boundaryId}/exceptions?exception=${waiverId}`
    );
  });
});
