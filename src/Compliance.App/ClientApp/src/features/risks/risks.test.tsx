// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { ProgramRisksPage } from './pages/program-risks.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const programId = '0190a1b2-0000-7000-8000-0000000000b1';
const riskId = '0190a1b2-0000-7000-8000-0000000000c1';
const program = `/api/v1/tenants/${tenantId}/programs/${programId}`;
const risk = `${program}/risks/${riskId}`;
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

function method(appetite: number | null = 8) {
  api.reply(`GET ${program}/risk-method`, 200, {
    tenant_id: tenantId,
    program_id: programId,
    method_version_id: 'v',
    version: 1,
    kind: 'qualitative_5x5',
    likelihood_scale: ['Rare', 'Unlikely', 'Possible', 'Likely', 'Almost certain'],
    impact_scale: ['Negligible', 'Minor', 'Moderate', 'Major', 'Severe'],
    appetite_threshold: appetite,
    reassessment_interval: 'P1Y',
    published_by: actor,
    published_at: '2026-09-01T00:00:00Z',
  });
}

function drafts() {
  api.reply(`${program}/risks`, 200, {
    items: [
      {
        tenant_id: tenantId,
        program_id: programId,
        risk_id: riskId,
        identifier: 'R-1',
        revision: 1,
        status: 'draft',
        owner_resolution: 'unresolved',
        content: { title: 'Credential theft', scenario: 'Phished admin', potential_effect: 'Data loss', source_note: null },
        last_changed_by_member_id: 'm',
        last_changed_by_display: 'Casey Lead',
        last_changed_at: '2026-09-02T00:00:00Z',
      },
    ],
    next_cursor: null,
  });
}

function assessment(phase: string, likelihood: number, impact: number, id = `a-${phase}`) {
  return {
    assessment_id: id,
    phase,
    method_version_id: 'v',
    method_version: 1,
    likelihood,
    impact,
    score: likelihood * impact,
    rationale: `${phase} rationale`,
    assessor: { kind: 'member', id: 'x', display: 'Riley Assessor' },
    assessed_at: '2026-09-03T00:00:00Z',
  };
}

function evaluation(revision: number, status: string, assessments: unknown[], treatment: unknown = null) {
  api.reply(`GET ${risk}/evaluation`, 200, {
    tenant_id: tenantId,
    program_id: programId,
    risk_id: riskId,
    revision,
    status,
    assessments,
    treatment,
    acceptances: [],
    reassessment_due_at: null,
    last_changed_by: actor,
    last_changed_at: '2026-09-03T00:00:00Z',
  });
}

function submit(container: HTMLElement, label: string) {
  container.querySelector(`form[aria-label="${label}"]`)!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
}

function fill(form: Element, labelStart: string, value: string) {
  const field = [...form.querySelectorAll('label')]
    .find((l) => l.textContent?.startsWith(labelStart))!
    .querySelector('input, textarea, select') as HTMLInputElement;
  field.value = value;
  field.dispatchEvent(new Event(field.tagName === 'SELECT' ? 'change' : 'input', { bubbles: true }));
}

beforeEach(async () => {
  clearActiveTenant();
  api = stubApi();
  api.reply('/api/v1/tenant-slugs/acme/mine', 200, { tenant_id: tenantId, current_slug: 'acme', redirect: false });
  api.reply('/api/v1/tenants/mine', 200, { items: [{ tenant_id: tenantId, name: 'Acme Corp', slug: 'acme' }], next_cursor: null });
  await resolveTenantRoute('acme', { pathname: `/acme/programs/${programId}/risks`, search: '', hash: '' });
});

afterEach(() => {
  vi.unstubAllGlobals();
  while (mounted.length > 0) mounted.pop()?.cleanup();
});

describe('risk assessment and treatment (R1-07 frontend #206)', () => {
  it('ShouldShowRiskMethodAssessmentsAndStatusGivenAnAssessedRisk', async () => {
    // Arrange
    method();
    drafts();
    evaluation(2, 'assessed', [assessment('inherent', 4, 4), assessment('target', 2, 3)]);

    // Act
    const container = mount(() => <ProgramRisksPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('.risk-assessments')).not.toBeNull());

    // Assert
    expect(container.querySelector('h1')?.textContent).toBe('Risks');
    expect(container.textContent).toContain('Version 1, qualitative 5×5');
    expect(container.textContent).toContain('R-1: Credential theft');
    expect(container.textContent).toContain('Status: Assessed');
    expect(container.textContent).toContain('16 (above appetite)');
    expect(container.textContent).toContain('6 (within appetite)');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldRecordAnAssessmentAgainstTheEvaluationRevisionAndMethodVersion', async () => {
    // Arrange
    method();
    drafts();
    evaluation(0, 'unassessed', []);
    api.reply(`POST ${risk}/assessments`, 200, assessment('inherent', 4, 5));
    const container = mount(() => <ProgramRisksPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Assess R-1"]')).not.toBeNull());
    const form = container.querySelector('form[aria-label="Assess R-1"]')!;
    fill(form, 'Likelihood', '4');
    fill(form, 'Impact', '5');
    fill(form, 'Rationale', 'Admins lack phishing-resistant MFA.');

    // Act
    submit(container, 'Assess R-1');
    await vi.waitFor(() => expect(api.bodies.some((b) => b.method === 'POST' && b.path === `${risk}/assessments`)).toBe(true));

    // Assert
    const sent = api.bodies.find((b) => b.method === 'POST' && b.path === `${risk}/assessments`)?.body;
    expect(sent).toEqual({
      expected_revision: 0,
      method_version: 1,
      phase: 'inherent',
      likelihood: 4,
      impact: 5,
      rationale: 'Admins lack phishing-resistant MFA.',
    });
  });

  it('ShouldChooseATreatmentWithRationale', async () => {
    // Arrange
    method();
    drafts();
    evaluation(1, 'assessed', [assessment('inherent', 4, 4)]);
    api.reply(`PUT ${risk}/treatment`, 204);
    const container = mount(() => <ProgramRisksPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Choose treatment for R-1"]')).not.toBeNull());
    const form = container.querySelector('form[aria-label="Choose treatment for R-1"]')!;
    fill(form, 'Treatment', 'transfer');
    fill(form, 'Treatment rationale', 'Cyber insurance covers this.');

    // Act
    submit(container, 'Choose treatment for R-1');
    await vi.waitFor(() => expect(api.bodies.some((b) => b.method === 'PUT' && b.path === `${risk}/treatment`)).toBe(true));

    // Assert
    expect(api.bodies.find((b) => b.method === 'PUT')?.body).toEqual({
      expected_revision: 1,
      kind: 'transfer',
      rationale: 'Cyber insurance covers this.',
    });
  });

  it('ShouldExplainAcceptanceRulesAndBlockALeadGivenResidualAboveAppetite', async () => {
    // Arrange
    method(8);
    drafts();
    evaluation(3, 'residual_assessed', [assessment('inherent', 4, 4), assessment('residual', 3, 4)], {
      kind: 'accept',
      rationale: 'Low cost of loss',
      chosen_by: actor,
      chosen_at: '2026-09-03T00:00:00Z',
    });

    // Act
    const container = mount(() => <ProgramRisksPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Accept R-1"]')).not.toBeNull());

    // Assert
    const form = container.querySelector('form[aria-label="Accept R-1"]')!;
    expect(form.textContent).toContain('personal decision');
    expect(form.textContent).toContain('expires within 12 months');
    expect(form.textContent).toContain('Residual score 12 is above appetite 8, so only an executive can accept this risk.');
    expect((form.querySelector('button[type="submit"]') as HTMLButtonElement).disabled).toBe(true);
    expect(container.textContent).toContain('Treatment: Accept');
  });

  it('ShouldShowForbiddenAcceptanceGivenTheCallerLacksTheAuthorityGrant', async () => {
    // Arrange
    method(20);
    drafts();
    evaluation(3, 'residual_assessed', [assessment('inherent', 4, 4), assessment('residual', 3, 4)]);
    api.reply(`POST ${risk}/acceptances`, 403, problem(403, 'Forbidden'));
    const container = mount(() => <ProgramRisksPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Accept R-1"]')).not.toBeNull());
    const form = container.querySelector('form[aria-label="Accept R-1"]')!;
    fill(form, 'Acceptance expires on', '2027-03-01');
    fill(form, 'Acceptance rationale', 'Within appetite.');

    // Act
    submit(container, 'Accept R-1');
    await vi.waitFor(() => expect(form.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(form.querySelector('[role="alert"]')?.textContent).toContain('You do not hold the compliance lead acceptance grant');
    const sent = api.bodies.find((b) => b.method === 'POST' && b.path === `${risk}/acceptances`)?.body as Record<string, unknown>;
    expect(sent).toMatchObject({
      expected_revision: 3,
      residual_assessment_id: 'a-residual',
      approver_authority: 'compliance_lead',
      expires_at: '2027-03-01T00:00:00Z',
    });
  });

  it('ShouldAskToReloadGivenAStaleEvaluationRevision', async () => {
    // Arrange
    method();
    drafts();
    evaluation(1, 'assessed', [assessment('inherent', 4, 4)]);
    api.reply(`PUT ${risk}/treatment`, 409, problem(409, 'The risk evaluation revision is stale.'));
    const container = mount(() => <ProgramRisksPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Choose treatment for R-1"]')).not.toBeNull());
    const form = container.querySelector('form[aria-label="Choose treatment for R-1"]')!;
    fill(form, 'Treatment rationale', 'Fix it.');

    // Act
    submit(container, 'Choose treatment for R-1');
    await vi.waitFor(() => expect(form.querySelector('[role="alert"]')).not.toBeNull());

    // Assert
    expect(form.querySelector('[role="alert"]')?.textContent).toContain('Someone else changed this risk');
  });

  it('ShouldOfferRetryGivenEvaluationProjectionLag', async () => {
    // Arrange
    method();
    drafts();
    api.reply(`GET ${risk}/evaluation`, 409, problem(409, 'Projection is behind.', true));

    // Act
    const container = mount(() => <ProgramRisksPage programId={programId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('still being processed'));

    // Assert
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Try again')).toBe(true);
  });

  it('ShouldInvitePublishingAMethodAndRecordingRisksGivenNone', async () => {
    // Arrange
    api.reply(`GET ${program}/risk-method`, 404, problem(404, 'No method.'));
    api.reply(`${program}/risks`, 200, { items: [], next_cursor: null });

    // Act
    const container = mount(() => <ProgramRisksPage programId={programId} />);
    await vi.waitFor(() => expect(container.textContent).toContain('No risks recorded yet'));

    // Assert
    expect(container.textContent).toContain('No risk method is published for this program yet');
    expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Publish risk method')).toBe(true);
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldShowForbiddenGivenTheViewerCannotManageTheProgram', async () => {
    // Arrange
    api.reply(`GET ${program}/risk-method`, 403, problem(403, 'Forbidden'));
    api.reply(`${program}/risks`, 403, problem(403, 'Forbidden'));

    // Act
    const container = mount(() => <ProgramRisksPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('h1')?.textContent).toBe('Risks are not available to you'));

    // Assert
    expect(container.querySelector('form')).toBeNull();
  });
});

describe('risk draft editing, history, and governance (R1-07 frontend b #482)', () => {
  it('ShouldReviseTheDraftAgainstItsRevisionGivenEditedContent', async () => {
    // Arrange
    method();
    drafts();
    evaluation(0, 'unassessed', []);
    api.reply(`PUT ${risk}/draft`, 204);
    const container = mount(() => <ProgramRisksPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Edit R-1"]')).not.toBeNull());
    const form = container.querySelector('form[aria-label="Edit R-1"]')!;
    fill(form, 'Title', 'Admin credential theft');
    fill(form, 'Source note', 'Workshop 2026-09-01');

    // Act
    submit(container, 'Edit R-1');
    await vi.waitFor(() => expect(api.bodies.some((b) => b.method === 'PUT' && b.path === `${risk}/draft`)).toBe(true));

    // Assert
    expect(api.bodies.find((b) => b.method === 'PUT' && b.path === `${risk}/draft`)?.body).toEqual({
      expected_revision: 1,
      title: 'Admin credential theft',
      scenario: 'Phished admin',
      potential_effect: 'Data loss',
      source_note: 'Workshop 2026-09-01',
    });
  });

  it('ShouldListDraftRevisionsGivenTheHistoryIsShown', async () => {
    // Arrange
    method();
    drafts();
    evaluation(0, 'unassessed', []);
    api.reply(`${risk}/evaluation/history`, 200, { items: [], next_cursor: null });
    api.reply(`${risk}/draft/revisions`, 200, {
      items: [1, 2].map((revision) => ({
        tenant_id: tenantId,
        program_id: programId,
        risk_id: riskId,
        identifier: 'R-1',
        revision,
        content: { title: revision === 1 ? 'Credential theft' : 'Admin credential theft', scenario: 'Phished admin', potential_effect: 'Data loss', source_note: null },
        changed_by_member_id: 'm',
        changed_by_display: 'Casey Lead',
        changed_at: '2026-09-02T00:00:00Z',
      })),
      next_cursor: null,
    });
    const container = mount(() => <ProgramRisksPage programId={programId} />);
    await vi.waitFor(() => expect([...container.querySelectorAll('button')].some((b) => b.textContent === 'Show history')).toBe(true));

    // Act
    [...container.querySelectorAll('button')].find((b) => b.textContent === 'Show history')!.click();
    await vi.waitFor(() => expect(container.querySelector('.risk-draft-history')).not.toBeNull());

    // Assert
    const items = [...container.querySelectorAll('.risk-draft-history li')].map((li) => li.textContent);
    expect(items[0]).toContain('Draft revision 2 by Casey Lead');
    expect(items[0]).toContain('Admin credential theft');
    expect(items[1]).toContain('Draft revision 1');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  function people() {
    api.reply(`/api/v1/tenants/${tenantId}/people`, 200, {
      items: [
        { person_id: 'p-1', revision: 1, display_name: 'Jordan Owner', work_email: null, source_kind: 'manual', last_changed_by: actor, last_changed_at: '2026-09-01T00:00:00Z' },
        { person_id: 'p-2', revision: 1, display_name: 'Sam Successor', work_email: null, source_kind: 'manual', last_changed_by: actor, last_changed_at: '2026-09-01T00:00:00Z' },
      ],
      next_cursor: null,
    });
  }

  function governance(treatmentStatus = 'proposed') {
    api.reply(`GET ${risk}/governance`, 200, {
      tenant_id: tenantId,
      program_id: programId,
      risk_id: riskId,
      revision: 4,
      owner: { person_id: 'p-1', correlated_member_id: null, rationale: 'Owns identity', assigned_by: actor, assigned_at: '2026-09-04T00:00:00Z' },
      control_treatments: [
        {
          treatment_id: 't-1',
          risk_id: riskId,
          control_id: 'c-1',
          control_version_id: 'cv-1',
          status: treatmentStatus,
          rationale: 'MFA control reduces likelihood',
          proposed_by: { kind: 'member', id: 'x', display: 'Riley Assessor' },
          proposed_at: '2026-09-05T00:00:00Z',
          review_decision_id: null,
          reviewed_by: null,
          review_rationale: null,
          reviewed_at: null,
          separation_of_duties_waiver_id: null,
          retired_by: null,
          retirement_rationale: null,
          retired_at: null,
        },
      ],
      reassessment_triggers: [
        { trigger_id: 'g-1', risk_id: riskId, trigger_kind: 'boundary_changed', source_reference: 'boundary v3', raised_at: '2026-09-06T00:00:00Z', status: 'open' },
      ],
    });
  }

  it('ShouldShowTheOwnerTriggersAndTreatmentsGivenGovernance', async () => {
    // Arrange
    method();
    drafts();
    people();
    governance();
    evaluation(0, 'unassessed', []);

    // Act
    const container = mount(() => <ProgramRisksPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('.risk-governance')).not.toBeNull());

    // Assert
    const section = container.querySelector('.risk-governance')!;
    expect(section.textContent).toContain('Owner: Jordan Owner');
    expect(section.textContent).toContain('Boundary changed (boundary v3)');
    expect(section.textContent).toContain('MFA control reduces likelihood');
    expect(section.textContent).toContain('Proposed by Riley Assessor');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldAssignAnOwnerAgainstTheGovernanceRevision', async () => {
    // Arrange
    method();
    drafts();
    people();
    governance();
    evaluation(0, 'unassessed', []);
    api.reply(`PUT ${risk}/owner`, 204);
    const container = mount(() => <ProgramRisksPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Assign owner for R-1"]')).not.toBeNull());
    const form = container.querySelector('form[aria-label="Assign owner for R-1"]')!;
    fill(form, 'Owner', 'p-2');
    fill(form, 'Owner rationale', 'Took over identity');

    // Act
    submit(container, 'Assign owner for R-1');
    await vi.waitFor(() => expect(api.bodies.some((b) => b.method === 'PUT' && b.path === `${risk}/owner`)).toBe(true));

    // Assert
    expect(api.bodies.find((b) => b.method === 'PUT' && b.path === `${risk}/owner`)?.body).toEqual({
      expected_revision: 4,
      person_id: 'p-2',
      rationale: 'Took over identity',
    });
  });

  it('ShouldReviewAProposedControlTreatmentAgainstTheGovernanceRevision', async () => {
    // Arrange
    method();
    drafts();
    people();
    governance();
    evaluation(0, 'unassessed', []);
    api.reply(`POST ${risk}/control-treatments/t-1/reviews`, 204);
    const container = mount(() => <ProgramRisksPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('form[aria-label="Review treatment t-1"]')).not.toBeNull());
    const form = container.querySelector('form[aria-label="Review treatment t-1"]')!;
    fill(form, 'Review outcome', 'reject');
    fill(form, 'Review rationale', 'Control does not cover admins');

    // Act
    submit(container, 'Review treatment t-1');
    await vi.waitFor(() => expect(api.bodies.some((b) => b.method === 'POST' && b.path === `${risk}/control-treatments/t-1/reviews`)).toBe(true));

    // Assert
    expect(api.bodies.find((b) => b.path === `${risk}/control-treatments/t-1/reviews`)?.body).toEqual({
      expected_revision: 4,
      outcome: 'reject',
      rationale: 'Control does not cover admins',
    });
  });

  it('ShouldNotOfferReviewGivenTheTreatmentWasAlreadyReviewed', async () => {
    // Arrange
    method();
    drafts();
    people();
    governance('accepted');
    evaluation(0, 'unassessed', []);

    // Act
    const container = mount(() => <ProgramRisksPage programId={programId} />);
    await vi.waitFor(() => expect(container.querySelector('.risk-governance')).not.toBeNull());

    // Assert
    expect(container.querySelector('form[aria-label="Review treatment t-1"]')).toBeNull();
    expect(container.querySelector('.risk-governance')!.textContent).toContain('Accepted');
  });
});
