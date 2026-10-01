// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { CommitmentDetailPage } from './pages/commitment-detail.js';
import { CommitmentExceptionsPage } from './pages/commitment-exceptions.js';
import { CommitmentsPage } from './pages/commitments-list.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const programId = '0190a1b2-0000-7000-8000-0000000000b1';
const draftId = '0190a1b2-0000-7000-8000-0000000000d1';
const cuecId = '0190a1b2-0000-7000-8000-0000000000d2';
const serviceId = '0190a1b2-0000-7000-8000-0000000000c1';
const reviewId = '0190a1b2-0000-7000-8000-0000000000e1';
const approvalId = '0190a1b2-0000-7000-8000-0000000000e2';
const controlId = '0190a1b2-0000-7000-8000-0000000000f9';
const base = `/api/v1/tenants/${tenantId}`;
const list = `${base}/programs/${programId}/commitment-drafts`;
const detail = `${list}/${draftId}`;
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

function draftBody(overrides: Record<string, unknown> = {}) {
  return {
    tenant_id: tenantId,
    program_id: programId,
    draft_id: draftId,
    service_id: serviceId,
    kind: 'service_commitment',
    identifier: 'SC-01',
    revision: 2,
    status: 'draft',
    source_resolution: 'unverified',
    owner_resolution: 'unresolved',
    applicability_resolution: 'unresolved',
    statement: 'We restore service within four hours.',
    context: 'Availability',
    source_reference: 'MSA 4.1',
    last_changed_by_member_id: 'author',
    last_changed_by_display: 'Avery Author',
    last_changed_at: '2026-09-20T00:00:00Z',
    ...overrides,
  };
}

function revision(number: number) {
  return {
    tenant_id: tenantId,
    program_id: programId,
    draft_id: draftId,
    service_id: serviceId,
    kind: 'service_commitment',
    identifier: 'SC-01',
    revision: number,
    statement: number === 1 ? 'Original statement' : 'We restore service within four hours.',
    context: 'Availability',
    source_reference: 'MSA 4.1',
    changed_by_member_id: 'author',
    changed_by_display: 'Avery Author',
    changed_at: '2026-09-20T00:00:00Z',
  };
}

function decision(stage: 'review' | 'approval', outcome: string, revisionNumber = 2) {
  return {
    tenant_id: tenantId,
    program_id: programId,
    draft_id: draftId,
    decision_id: stage === 'review' ? reviewId : approvalId,
    revision: revisionNumber,
    outcome,
    owner_reference: stage === 'review' ? 'Ops lead' : null,
    applicability: stage === 'review' ? 'applicable' : null,
    interpretation: stage === 'review' ? 'supported' : null,
    interpretation_note: null,
    rationale: stage === 'review' ? 'Matches the contract.' : 'Approved for the engagement.',
    version: stage === 'approval' ? 1 : null,
    effective_from: stage === 'approval' ? '2026-10-01' : null,
    impact_digest: stage === 'approval' ? 'digest-1' : null,
    actor_member_id: stage === 'review' ? 'reviewer' : 'approver',
    actor_display: stage === 'review' ? 'Riley Reviewer' : 'Pat Approver',
    decided_at: stage === 'review' ? '2026-09-21T00:00:00Z' : '2026-09-22T00:00:00Z',
    separation_of_duties_waiver_id: null,
    stage,
    accepted_review_decision_id: stage === 'approval' ? reviewId : null,
    source_verification: stage === 'review' && outcome === 'accept' ? 'verified' : null,
    source_verified_reference: null,
    source_evidence: stage === 'review' ? 'Signed MSA v3' : null,
  };
}

function versionBody() {
  return {
    tenant_id: tenantId,
    program_id: programId,
    draft_id: draftId,
    service_id: serviceId,
    kind: 'service_commitment',
    identifier: 'SC-01',
    version: 1,
    revision: 1,
    statement: 'Original statement',
    context: 'Availability',
    source_reference: 'MSA 4.1',
    owner_reference: 'Ops lead',
    applicability: 'applicable',
    interpretation: 'unsupported',
    interpretation_note: 'Legal has not confirmed the clause.',
    performed_by: 'service_organization',
    internally_performed: true,
    effective_from: '2026-09-01',
    decision: decision('review', 'accept', 1),
    source_resolution: 'verified',
    source_evidence: 'Signed MSA v3',
    approval: decision('approval', 'approve', 1),
  };
}

function preview(digest = 'digest-1') {
  return {
    tenant_id: tenantId,
    program_id: programId,
    draft_id: draftId,
    revision: 2,
    effective_version: 1,
    changes: [{ field: 'statement', before: 'Original statement', after: 'We restore service within four hours.' }],
    dependents: [{ context: 'controls', record_type: 'control', record_id: controlId, relationship: 'approved_applicability' }],
    unlinked_contexts: [
      { context: 'risks', reason: 'Risk drafts do not reference commitments yet.' },
      { context: 'evidence', reason: 'Evidence records do not reference commitments yet.' },
    ],
    complete: true,
    digest,
  };
}

function detailAnswers(options: { decisions?: unknown[]; draft?: Record<string, unknown> } = {}) {
  api.reply(`GET ${detail}`, 200, draftBody(options.draft));
  api.reply(`GET ${detail}/revisions`, 200, { items: [revision(1), revision(2)], next_cursor: null });
  api.reply(`GET ${detail}/versions`, 200, { items: [versionBody()], next_cursor: null });
  api.reply(`GET ${detail}/decisions`, 200, { items: options.decisions ?? [], next_cursor: null });
  api.reply(`GET ${detail}/effective-version`, 200, versionBody());
  api.reply(`GET ${detail}/impact-preview`, 200, preview());
  api.reply(`GET ${base}/members`, 200, { items: [], next_cursor: null });
  api.reply(`GET ${base}/responsibilities`, 200, {
    tenant_id: tenantId,
    set_id: 's',
    scope: { record_type: 'commitment', record_id: draftId, version_id: draftId, revision: 2 },
    revision: 0,
    assignments: [],
  });
}

function field(container: HTMLElement, label: string) {
  return [...container.querySelectorAll('label')].find((l) => l.textContent?.trim().startsWith(label))!
    .querySelector('input, textarea, select') as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement;
}

function type(element: HTMLInputElement | HTMLTextAreaElement, value: string) {
  element.value = value;
  element.dispatchEvent(new Event('input', { bubbles: true }));
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

describe('commitment authoring (R1-13 frontend #230)', () => {
  it('ShouldListCommitmentsByKindAndMarkCarveOuts', async () => {
    // Arrange
    api.reply(`GET ${list}`, 200, {
      items: [draftBody(), draftBody({ draft_id: cuecId, kind: 'user_entity_responsibility', identifier: 'CUEC-01' })],
      next_cursor: null,
    });
    api.reply(`GET ${base}/programs/${programId}/client-services`, 200, { items: [], next_cursor: null });

    // Act
    const container = mount(() => <CommitmentsPage programId={programId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('CUEC-01'));

    // Assert
    expect(container.querySelector(`a[href="/acme/programs/${programId}/commitments/${draftId}"]`)).not.toBeNull();
    expect(container.textContent).toContain('Complementary user entity controls (CUECs)');
    expect(container.textContent).toContain('never counted as an internally performed control');
    expect(container.textContent).toContain('Record an active client service');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldCreateADraftForTheChosenKindAndService', async () => {
    // Arrange
    api.reply(`GET ${list}`, 200, { items: [], next_cursor: null });
    api.reply(`GET ${base}/programs/${programId}/client-services`, 200, {
      items: [{ tenant_id: tenantId, service_id: serviceId, revision: 1, name: 'Payments', purpose: 'p', owner_reference: 'o', status: 'active', last_changed_by_member_id: 'a', last_changed_by_display: 'A', last_changed_at: '2026-09-20T00:00:00Z' }],
      next_cursor: null,
    });
    api.reply(`POST ${list}`, 409, problem(409, 'The draft identifier already exists.'));
    const container = mount(() => <CommitmentsPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('option[value="' + serviceId + '"]')).not.toBeNull());
    const kind = field(container, 'Kind') as HTMLSelectElement;
    kind.value = 'subservice_responsibility';
    kind.dispatchEvent(new Event('change', { bubbles: true }));
    type(field(container, 'Identifier') as HTMLInputElement, 'CSOC-01');
    type(field(container, 'Statement') as HTMLTextAreaElement, 'The hosting provider patches hypervisors.');
    type(field(container, 'Source reference') as HTMLInputElement, 'Hosting SOC 2 report');

    // Act
    submit(container.querySelector('.commitment-form')!);
    await vi.waitFor(() => expect(container.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    const sent = api.bodies.find((b) => b.method === 'POST' && b.path === list)?.body as Record<string, string>;
    expect(sent.kind).toBe('subservice_responsibility');
    expect(sent.service_id).toBe(serviceId);
    expect(sent.identifier).toBe('CSOC-01');
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('Someone else changed this commitment');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldShowForbiddenGivenTheViewerCannotReadCommitments', async () => {
    // Arrange
    api.reply(`GET ${list}`, 403, problem(403, 'Forbidden'));

    // Act
    const container = mount(() => <CommitmentsPage programId={programId} />);

    // Assert
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Commitments are not available to you'));
    expect(container.querySelector('form')).toBeNull();
  });

  it('ShouldReviseAgainstTheExpectedRevisionAndExplainStaleConflicts', async () => {
    // Arrange
    detailAnswers();
    api.reply(`PUT ${detail}`, 409, problem(409, 'The commitment draft revision is stale.'));
    const container = mount(() => <CommitmentDetailPage programId={programId} draftId={draftId} />);
    await vi.waitFor(() => expect(container.querySelector('.commitment-form')).not.toBeNull());
    type(field(container, 'Statement') as HTMLTextAreaElement, 'Revised statement');

    // Act
    submit(container.querySelector('.commitment-form')!);
    await vi.waitFor(() => expect(container.querySelector('.commitment-form [role="alert"]')).not.toBeNull());

    // Assert
    const sent = api.bodies.find((b) => b.method === 'PUT' && b.path === detail)?.body as { expected_revision: number; statement: string };
    expect(sent.expected_revision).toBe(2);
    expect(sent.statement).toBe('Revised statement');
    expect(container.querySelector('.commitment-form [role="alert"]')?.textContent).toContain('Someone else changed this commitment');
  });

  it('ShouldExplainProjectionLagGivenATransientConflict', async () => {
    // Arrange
    api.reply(`GET ${detail}`, 409, problem(409, 'Projection is behind.', true));

    // Act
    const container = mount(() => <CommitmentDetailPage programId={programId} draftId={draftId} />);
    await vi.waitFor(() => expect(container.querySelector('h1')).not.toBeNull());

    // Assert
    expect(container.textContent).toContain('still being processed');
    expect(button(container, 'Try again')).not.toBeUndefined();
  });

  it('ShouldShowNotFoundGivenACommitmentFromAnotherOrganization', async () => {
    // Arrange
    api.reply(`GET ${detail}`, 404, problem(404, 'The draft was not found.'));

    // Act
    const container = mount(() => <CommitmentDetailPage programId={programId} draftId={draftId} />);

    // Assert
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Commitment not found'));
  });

  it('ShouldShowHistoryEffectiveVersionAndImpactWithUnlinkedReasons', async () => {
    // Arrange
    detailAnswers();

    // Act
    const container = mount(() => <CommitmentDetailPage programId={programId} draftId={draftId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Risk drafts do not reference commitments yet.'));
    await vi.waitFor(() => expect(container.textContent).toContain('Legal has not confirmed the clause.'));

    // Assert
    expect(container.querySelector('.commitment-dependents')?.textContent).toContain('approved applicability');
    expect(container.textContent).toContain('Authored since the last effective version by Avery Author');
    expect(container.textContent).toContain('Reviewed by Riley Reviewer, approved by Pat Approver');
    expect(container.textContent).toContain('Approval needs an accepted review of this revision first.');
    expect(button(container, 'Approve commitment')?.disabled).toBe(true);
    expect(await accessibilityViolations(container)).toEqual([]);
  });
});

describe('commitment review and approval (R1-13 frontend #230)', () => {
  it('ShouldSendOwnerApplicabilityInterpretationAndSourceVerificationOnAccept', async () => {
    // Arrange
    detailAnswers();
    api.reply(`POST ${detail}/reviews`, 204);
    const container = mount(() => <CommitmentDetailPage programId={programId} draftId={draftId} />);
    await vi.waitFor(() => expect(container.querySelector('.commitment-review')).not.toBeNull());
    type(field(container, 'Verified owner') as HTMLInputElement, 'Ops lead');
    type(field(container, 'Source reference you verified') as HTMLInputElement, 'MSA 4.1');
    type(field(container, 'Source evidence checked') as HTMLTextAreaElement, 'Signed MSA v3, section 4.1');
    type(field(container, 'Review rationale') as HTMLTextAreaElement, 'Matches the contract.');

    // Act
    submit(container.querySelector('.commitment-review')!);
    await vi.waitFor(() => expect(api.bodies.some((b) => b.path === `${detail}/reviews`)).toBe(true));

    // Assert
    const sent = api.bodies.find((b) => b.path === `${detail}/reviews`)?.body as Record<string, unknown>;
    expect(sent).toMatchObject({
      expected_revision: 2,
      outcome: 'accept',
      owner_reference: 'Ops lead',
      applicability: 'applicable',
      interpretation: 'supported',
      source_verified_reference: 'MSA 4.1',
      source_evidence: 'Signed MSA v3, section 4.1',
    });
  });

  it('ShouldApproveCitingTheAcceptedReviewAndAcknowledgedDigest', async () => {
    // Arrange
    detailAnswers({ decisions: [decision('review', 'accept')], draft: { status: 'reviewed', source_resolution: 'verified' } });
    api.reply(`POST ${detail}/approvals`, 204);
    const container = mount(() => <CommitmentDetailPage programId={programId} draftId={draftId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Cites the accepted review by Riley Reviewer'));
    await vi.waitFor(() => expect(container.textContent).toContain('approved applicability'));
    expect(container.textContent).toContain('Riley Reviewer accepted this revision, so they cannot also approve it');
    check(field(container, 'I reviewed the current impact preview') as HTMLInputElement);
    type(field(container, 'Approval rationale') as HTMLTextAreaElement, 'Approved for the engagement.');

    // Act
    submit(container.querySelector('.commitment-approve')!);
    await vi.waitFor(() => expect(api.bodies.some((b) => b.path === `${detail}/approvals`)).toBe(true));

    // Assert
    const sent = api.bodies.find((b) => b.path === `${detail}/approvals`)?.body as Record<string, unknown>;
    expect(sent).toMatchObject({ expected_revision: 2, accepted_review_decision_id: reviewId, impact_digest: 'digest-1' });
  });

  it('ShouldExplainSeparationOfDutiesGivenADeniedApproval', async () => {
    // Arrange
    detailAnswers({ decisions: [decision('review', 'accept')], draft: { status: 'reviewed' } });
    api.reply(`POST ${detail}/approvals`, 403, problem(403, 'The accepted reviewer cannot also approve the same revision.'));
    const container = mount(() => <CommitmentDetailPage programId={programId} draftId={draftId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('approved applicability'));
    check(field(container, 'I reviewed the current impact preview') as HTMLInputElement);
    type(field(container, 'Approval rationale') as HTMLTextAreaElement, 'Approving my own review.');

    // Act
    submit(container.querySelector('.commitment-approve')!);
    await vi.waitFor(() => expect(container.textContent).toContain('This decision was denied.'));

    // Assert
    expect(container.textContent).toContain('Separation of duties blocks authors, the accepted reviewer');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldReloadImpactGivenAChangedPreviewOnApproval', async () => {
    // Arrange
    detailAnswers({ decisions: [decision('review', 'accept')], draft: { status: 'reviewed' } });
    api.reply(`POST ${detail}/approvals`, 409, problem(409, 'The impact preview changed. Reload it before approval.'));
    const container = mount(() => <CommitmentDetailPage programId={programId} draftId={draftId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('approved applicability'));
    check(field(container, 'I reviewed the current impact preview') as HTMLInputElement);
    type(field(container, 'Approval rationale') as HTMLTextAreaElement, 'Approve.');
    const previews = () => api.requested.filter((path) => path === `${detail}/impact-preview`).length;
    const before = previews();

    // Act
    submit(container.querySelector('.commitment-approve')!);
    await vi.waitFor(() => expect(container.textContent).toContain('The impact preview was reloaded'));

    // Assert
    await vi.waitFor(() => expect(previews()).toBeGreaterThan(before));
  });

  it('ShouldRecordAnExceptionScopedToTheCurrentRevision', async () => {
    // Arrange
    api.reply(`GET ${detail}`, 200, draftBody());
    api.reply(`GET ${base}/members`, 200, { items: [], next_cursor: null });

    // Act
    const container = mount(() => <CommitmentExceptionsPage programId={programId} draftId={draftId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('Applies only to revision 2'));

    // Assert
    expect(container.querySelector('h1')?.textContent).toBe('Separation-of-duties exceptions');
    expect(await accessibilityViolations(container)).toEqual([]);
  });
});
