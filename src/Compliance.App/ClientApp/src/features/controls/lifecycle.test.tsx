// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { ControlDetailPage } from './pages/control-detail.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const programId = '0190a1b2-0000-7000-8000-0000000000b1';
const controlId = '0190a1b2-0000-7000-8000-0000000000c1';
const versionId = '0190a1b2-0000-7000-8000-0000000000d1';
const successorId = '0190a1b2-0000-7000-8000-0000000000d2';
const retirementId = '0190a1b2-0000-7000-8000-0000000000d3';
const reviewId = '0190a1b2-0000-7000-8000-0000000000f1';
const base = `/api/v1/tenants/${tenantId}`;
const control = `${base}/programs/${programId}/controls/${controlId}`;
let api: ReturnType<typeof stubApi>;
const mounted: RenderResult[] = [];

function mount(component: () => unknown): HTMLElement {
  const result = render(component as never);
  mounted.push(result);
  return result.container;
}

function problem(status: number, detail: string, transient = false) {
  return {
    type: 'about:blank',
    title: 'Problem',
    status,
    detail,
    instance: '/',
    transient,
  };
}

const content = {
  title: 'Access reviews',
  objective: 'Access stays appropriate.',
  description: 'Quarterly access review of production systems.',
  implementation_narrative: 'The platform lead reviews access every quarter.',
  expected_evidence_descriptions: ['Signed quarterly review'],
  owner_reference: 'Platform lead',
  applicability: [],
};

const actor = { kind: 'member', id: 'm', display: 'Riley Reviewer' };

function approvedVersion(
  status = 'approved',
  effectiveUntil: string | null = null
) {
  return {
    tenant_id: tenantId,
    program_id: programId,
    control_id: controlId,
    identifier: 'AC-01',
    version_id: versionId,
    revision: 2,
    status,
    content_origin: 'organization_authored',
    content,
    effective_from: '2026-09-01',
    predecessor_version_id: null,
    owner_assignment_id: 'a',
    owner_member_id: 'o',
    owner_resolution: 'verified_member',
    accepted_review_decision_id: reviewId,
    approval_decision_id: 'x',
    approved_by: { kind: 'member', id: 'p', display: 'Pat Approver' },
    approval_rationale: 'Ready.',
    approved_at: '2026-09-01T00:00:00Z',
    separation_of_duties_waiver_id: null,
    effective_until: effectiveUntil,
  };
}

function decision(
  outcome: string,
  revision: number,
  target: string,
  decidedAt = '2026-09-25T00:00:00Z',
  id = reviewId
) {
  return {
    tenant_id: tenantId,
    program_id: programId,
    control_id: controlId,
    decision_id: id,
    version_id: target,
    revision,
    kind: 'review',
    outcome,
    actor,
    rationale: 'Looks right.',
    decided_at: decidedAt,
    supersedes_decision_id: null,
    relies_on_decision_id: null,
    separation_of_duties_waiver_id: null,
  };
}

function preview(kind: 'successor' | 'retirement', complete = true) {
  return {
    tenant_id: tenantId,
    program_id: programId,
    control_id: controlId,
    kind,
    target_id: kind === 'successor' ? successorId : retirementId,
    revision: 3,
    current_version_id: versionId,
    changes: [
      {
        field: 'title',
        change_type: 'revised',
        entry_id: null,
        previous_value: 'Access reviews',
        proposed_value: 'Quarterly access reviews',
      },
    ],
    contributions: [
      {
        context: 'mappings',
        status: 'linked',
        freshness: 'current',
        records: [
          {
            tenant_id: tenantId,
            context: 'mappings',
            record_type: 'control_mapping',
            record_id: 'r',
            version_id: null,
            reason: 'cites CC6.1',
          },
        ],
        complete: true,
      },
    ],
    pending_contexts: complete ? [] : ['evidence'],
    complete,
    digest: 'sha256-abcdef0123456789',
  };
}

function answers(options: {
  revision: number;
  approved: boolean;
  decisions?: unknown[];
  impact?: unknown;
  retired?: boolean;
}) {
  api.reply(`GET ${control}/draft`, 200, {
    tenant_id: tenantId,
    program_id: programId,
    control_id: controlId,
    identifier: 'AC-01',
    revision: options.revision,
    status: 'draft',
    owner_resolution: 'declared_unverified',
    applicability_resolution: 'declared',
    content,
    last_changed_by_member_id: 'm',
    last_changed_by_display: 'Casey Lead',
    last_changed_at: '2026-09-20T00:00:00Z',
  });
  api.reply(`GET ${control}/draft/revisions`, 200, {
    items: [],
    next_cursor: null,
  });
  if (options.approved) {
    const version = options.retired
      ? approvedVersion('retired', '2026-12-31')
      : approvedVersion();
    api.reply(`GET ${control}/current-version`, 200, version);
    api.reply(`GET ${control}/versions`, 200, {
      items: [version],
      next_cursor: null,
    });
    api.reply(`GET ${control}/effective-version`, 200, version);
  } else {
    api.reply(
      `GET ${control}/current-version`,
      404,
      problem(404, 'The control has no approved version.')
    );
    api.reply(`GET ${control}/versions`, 200, { items: [], next_cursor: null });
    api.reply(
      `GET ${control}/effective-version`,
      404,
      problem(404, 'No version.')
    );
  }
  api.reply(`GET ${control}/decisions`, 200, {
    items: options.decisions ?? [],
    next_cursor: null,
  });
  if (options.impact)
    api.reply(`GET ${control}/impact-preview`, 200, options.impact);
  else
    api.reply(
      `GET ${control}/impact-preview`,
      409,
      problem(
        409,
        'Impact preview requires a pending successor or retirement of an approved control.'
      )
    );
  api.reply(
    `GET ${base}/programs/${programId}`,
    404,
    problem(404, 'Program not found.')
  );
}

function form(container: HTMLElement, name: string) {
  return container.querySelector(
    `form[aria-label="${name}"]`
  ) as HTMLFormElement;
}

function field(root: HTMLElement, label: string) {
  return [...root.querySelectorAll('label')]
    .find((l) => l.querySelector('span')?.textContent === label)!
    .querySelector('input, textarea') as HTMLInputElement | HTMLTextAreaElement;
}

function type(element: HTMLInputElement | HTMLTextAreaElement, value: string) {
  element.value = value;
  element.dispatchEvent(new Event('input', { bubbles: true }));
}

function submit(element: HTMLFormElement) {
  element.dispatchEvent(
    new Event('submit', { bubbles: true, cancelable: true })
  );
}

function sent(method: string, path: string) {
  return api.bodies.find((b) => b.method === method && b.path === path)
    ?.body as Record<string, unknown> | undefined;
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
  await resolveTenantRoute('acme', {
    pathname: '/acme/programs',
    search: '',
    hash: '',
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('control review and lifecycle (R1-05 frontend b #480)', () => {
  it('ShouldRecordARequestForChangesAgainstTheExactRevision', async () => {
    // Arrange
    answers({ revision: 2, approved: false });
    api.reply(`POST ${control}/draft/reviews`, 204);
    const container = mount(() => (
      <ControlDetailPage programId={programId} controlId={controlId} />
    ));
    await vi.waitFor(() =>
      expect(form(container, 'Review draft')).not.toBeNull()
    );
    const review = form(container, 'Review draft');
    const radios = review.querySelectorAll('input[type="radio"]');
    (radios[1] as HTMLInputElement).click();
    type(field(review, 'Rationale'), 'Evidence list is incomplete.');

    // Act
    submit(review);
    await vi.waitFor(() =>
      expect(review.querySelector('[role="status"]')).not.toBeNull()
    );

    // Assert
    expect(sent('POST', `${control}/draft/reviews`)).toEqual({
      expected_revision: 2,
      outcome: 'request_changes',
      rationale: 'Evidence list is incomplete.',
    });
    expect(container.textContent).toContain('not in effect yet');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldBlockApprovalGivenNoAcceptedReviewOfThisRevision', async () => {
    // Arrange: the accepted review cites an earlier revision; the latest review requested changes.
    answers({
      revision: 3,
      approved: false,
      decisions: [decision('accept', 2, successorId, '2026-09-20T00:00:00Z')],
    });

    // Act
    const container = mount(() => (
      <ControlDetailPage programId={programId} controlId={controlId} />
    ));
    await vi.waitFor(() =>
      expect(form(container, 'Approve control')).not.toBeNull()
    );

    // Assert
    const button = form(container, 'Approve control').querySelector(
      'button[type="submit"]'
    ) as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    expect(container.textContent).toContain(
      'needs the latest review of revision 3 to accept it first'
    );
  });

  it('ShouldApproveCitingTheAcceptedReviewAndCitedWaiver', async () => {
    // Arrange
    answers({
      revision: 2,
      approved: false,
      decisions: [decision('accept', 2, successorId)],
    });
    api.reply(`POST ${control}/draft/approvals`, 204);
    const container = mount(() => (
      <ControlDetailPage programId={programId} controlId={controlId} />
    ));
    await vi.waitFor(() =>
      expect(container.textContent).toContain(
        'Cites the accepted review by Riley Reviewer'
      )
    );
    const approve = form(container, 'Approve control');
    type(field(approve, 'Effective from'), '2026-10-01');
    type(field(approve, 'Rationale'), 'Reviewed and owned.');
    type(
      field(approve, 'Separation-of-duties waiver ID (optional)'),
      '0190a1b2-0000-7000-8000-0000000000aa'
    );

    // Act
    submit(approve);
    await vi.waitFor(() =>
      expect(sent('POST', `${control}/draft/approvals`)).toBeDefined()
    );

    // Assert
    expect(sent('POST', `${control}/draft/approvals`)).toEqual({
      expected_revision: 2,
      accepted_review_decision_id: reviewId,
      effective_from: '2026-10-01',
      rationale: 'Reviewed and owned.',
      impact_digest: null,
      separation_of_duties_waiver_id: '0190a1b2-0000-7000-8000-0000000000aa',
    });
    expect(container.textContent).toContain(
      'Approval verifies that an active client-personnel member is assigned as control owner'
    );
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldExplainTheServerReasonGivenOwnerVerificationFails', async () => {
    // Arrange
    answers({
      revision: 2,
      approved: false,
      decisions: [decision('accept', 2, successorId)],
    });
    api.reply(
      `POST ${control}/draft/approvals`,
      409,
      problem(
        409,
        'Activation requires an active client-personnel member assigned control_owner on this exact draft revision.'
      )
    );
    const container = mount(() => (
      <ControlDetailPage programId={programId} controlId={controlId} />
    ));
    await vi.waitFor(() =>
      expect(form(container, 'Approve control')).not.toBeNull()
    );
    const approve = form(container, 'Approve control');
    type(field(approve, 'Rationale'), 'Ready.');

    // Act
    submit(approve);
    await vi.waitFor(() =>
      expect(approve.querySelector('[role="alert"]')).not.toBeNull()
    );

    // Assert
    expect(approve.querySelector('[role="alert"]')?.textContent).toContain(
      'assigned control_owner'
    );
  });

  it('ShouldAskToReloadGivenAStaleReviewRevision', async () => {
    // Arrange
    answers({ revision: 2, approved: false });
    api.reply(
      `POST ${control}/draft/reviews`,
      409,
      problem(
        409,
        'The control draft changed. Current revision: 3. Reload it and retry.'
      )
    );
    const container = mount(() => (
      <ControlDetailPage programId={programId} controlId={controlId} />
    ));
    await vi.waitFor(() =>
      expect(form(container, 'Review draft')).not.toBeNull()
    );
    const review = form(container, 'Review draft');
    type(field(review, 'Rationale'), 'Fine.');

    // Act
    submit(review);
    await vi.waitFor(() =>
      expect(review.querySelector('[role="alert"]')).not.toBeNull()
    );

    // Assert
    expect(review.querySelector('[role="alert"]')?.textContent).toContain(
      'Someone else changed this control'
    );
  });

  it('ShouldApproveASuccessorWithTheAcknowledgedImpactDigest', async () => {
    // Arrange
    answers({
      revision: 3,
      approved: true,
      impact: preview('successor'),
      decisions: [decision('accept', 3, successorId)],
    });
    api.reply(`POST ${control}/draft/approvals`, 204);
    const container = mount(() => (
      <ControlDetailPage programId={programId} controlId={controlId} />
    ));
    await vi.waitFor(() =>
      expect(container.textContent).toContain('Impact of this successor')
    );
    const approve = form(container, 'Approve control');
    type(field(approve, 'Rationale'), 'Impact acknowledged.');

    // Act
    submit(approve);
    await vi.waitFor(() =>
      expect(sent('POST', `${control}/draft/approvals`)).toBeDefined()
    );

    // Assert
    expect(sent('POST', `${control}/draft/approvals`)?.impact_digest).toBe(
      'sha256-abcdef0123456789'
    );
    expect(container.textContent).toContain(
      'becomes “Quarterly access reviews”'
    );
    expect(container.textContent).toContain(
      'a successor draft awaits review and approval'
    );
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldBlockSuccessorApprovalGivenAnIncompleteImpactPreview', async () => {
    // Arrange
    answers({
      revision: 3,
      approved: true,
      impact: preview('successor', false),
      decisions: [decision('accept', 3, successorId)],
    });

    // Act
    const container = mount(() => (
      <ControlDetailPage programId={programId} controlId={controlId} />
    ));
    await vi.waitFor(() =>
      expect(form(container, 'Approve control')).not.toBeNull()
    );

    // Assert
    const button = form(container, 'Approve control').querySelector(
      'button[type="submit"]'
    ) as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    expect(container.textContent).toContain('still waiting on evidence');
  });

  it('ShouldProposeRetirementAndSuccessorOnlyFromTheApprovedVersion', async () => {
    // Arrange
    answers({ revision: 2, approved: true });
    api.reply(`POST ${control}/retirement-proposals`, 200, {
      control_id: controlId,
      retirement_id: retirementId,
      version_id: versionId,
      revision: 2,
    });
    const container = mount(() => (
      <ControlDetailPage programId={programId} controlId={controlId} />
    ));
    await vi.waitFor(() =>
      expect(form(container, 'Propose retirement')).not.toBeNull()
    );
    const retire = form(container, 'Propose retirement');
    type(field(retire, 'Retire effective'), '2026-12-31');
    type(field(retire, 'Why retire this control'), 'Replaced by SSO reviews.');

    // Act
    submit(retire);
    await vi.waitFor(() =>
      expect(sent('POST', `${control}/retirement-proposals`)).toBeDefined()
    );

    // Assert
    expect(sent('POST', `${control}/retirement-proposals`)).toEqual({
      expected_approved_version_id: versionId,
      effective_until: '2026-12-31',
      rationale: 'Replaced by SSO reviews.',
    });
    expect(container.textContent).toContain('Propose a successor');
    expect(container.textContent).not.toContain('Edit draft');
    expect(
      container.querySelector('.control-versions li')?.textContent
    ).toContain('effective 2026-09-01');
    expect(
      container.querySelector('.control-effective')?.textContent
    ).toContain('Revision 2');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldProposeASuccessorCitingTheApprovedVersion', async () => {
    // Arrange
    answers({ revision: 2, approved: true });
    api.reply(`POST ${control}/successors`, 200, {
      control_id: controlId,
      draft_version_id: successorId,
      predecessor_version_id: versionId,
      revision: 3,
    });
    const container = mount(() => (
      <ControlDetailPage programId={programId} controlId={controlId} />
    ));
    await vi.waitFor(() =>
      expect(container.textContent).toContain('Propose a successor')
    );
    const successorForm = [...container.querySelectorAll('form')].find((f) =>
      f.textContent?.includes('Propose successor')
    )!;
    type(field(successorForm, 'Title'), 'Quarterly access reviews');

    // Act
    submit(successorForm);
    await vi.waitFor(() =>
      expect(sent('POST', `${control}/successors`)).toBeDefined()
    );

    // Assert
    const body = sent('POST', `${control}/successors`) as {
      expected_approved_version_id: string;
      content: { title: string };
    };
    expect(body.expected_approved_version_id).toBe(versionId);
    expect(body.content.title).toBe('Quarterly access reviews');
  });

  it('ShouldApproveRetirementWithTheReviewOfTheRetirementProposal', async () => {
    // Arrange: an older accept of the activated draft at the same revision must not be cited.
    answers({
      revision: 2,
      approved: true,
      impact: preview('retirement'),
      decisions: [
        decision(
          'accept',
          2,
          retirementId,
          '2026-09-28T00:00:00Z',
          '0190a1b2-0000-7000-8000-0000000000f2'
        ),
        decision('accept', 2, versionId, '2026-08-28T00:00:00Z'),
      ],
    });
    api.reply(`POST ${control}/retirements`, 204);
    const container = mount(() => (
      <ControlDetailPage programId={programId} controlId={controlId} />
    ));
    await vi.waitFor(() =>
      expect(form(container, 'Approve retirement')).not.toBeNull()
    );
    const retire = form(container, 'Approve retirement');
    type(field(retire, 'Rationale'), 'Superseded.');

    // Act
    submit(retire);
    await vi.waitFor(() =>
      expect(sent('POST', `${control}/retirements`)).toBeDefined()
    );

    // Assert
    expect(sent('POST', `${control}/retirements`)).toEqual({
      expected_revision: 2,
      accepted_review_decision_id: '0190a1b2-0000-7000-8000-0000000000f2',
      impact_digest: 'sha256-abcdef0123456789',
      rationale: 'Superseded.',
    });
    expect(container.textContent).toContain('A retirement is proposed');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldShowRetiredControlsWithoutDecisionForms', async () => {
    // Arrange
    answers({ revision: 2, approved: true, retired: true });

    // Act
    const container = mount(() => (
      <ControlDetailPage programId={programId} controlId={controlId} />
    ));
    await vi.waitFor(() =>
      expect(container.textContent).toContain('Retired effective 2026-12-31')
    );

    // Assert
    expect(container.querySelector('form')).toBeNull();
    expect(
      container.querySelector('.control-versions li')?.textContent
    ).toContain('until 2026-12-31');
  });

  it('ShouldOfferRetryGivenTheLifecycleCannotLoad', async () => {
    // Arrange
    answers({ revision: 2, approved: false });
    api.reply(
      `GET ${control}/decisions`,
      409,
      problem(409, 'Projection is behind.', true)
    );

    // Act
    const container = mount(() => (
      <ControlDetailPage programId={programId} controlId={controlId} />
    ));
    await vi.waitFor(() =>
      expect(container.textContent).toContain('still being processed')
    );

    // Assert
    expect(
      [...container.querySelectorAll('button')].some(
        (b) => b.textContent === 'Try again'
      )
    ).toBe(true);
  });
});
