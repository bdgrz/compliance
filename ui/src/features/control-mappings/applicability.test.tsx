// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import type { Program } from '../programs/programs.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { CoverageCard } from './coverage-card.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const programId = '0190a1b2-0000-7000-8000-0000000000b1';
const controlId = '0190a1b2-0000-7000-8000-0000000000c1';
const versionId = '0190a1b2-0000-7000-8000-0000000000d1';
const editionId = '0190a1b2-0000-7000-8000-0000000000e9';
const mappingId = '0190a1b2-0000-7000-8000-0000000000a1';
const decisionId = '0190a1b2-0000-7000-8000-0000000000f1';
const base = `/api/v1/tenants/${tenantId}`;
const programPath = `${base}/programs/${programId}`;
const applicability = `${programPath}/criterion-applicability`;
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

const program: Program = {
  programId,
  name: 'SOC 2 2026',
  stage: 'readiness',
  nextStage: 'type_i',
  revision: 3,
  plan: {
    target_readiness_date: null,
    target_type_i_as_of_date: null,
    target_type_ii_start_date: null,
    target_type_ii_end_date: null,
    readiness_advisor: null,
    audit_firm: null,
  },
  stagePlan: [],
  lastChangedBy: 'Casey Lead',
  lastChangedAt: '2026-09-20T00:00:00Z',
  criteriaEditionId: editionId,
};

const person = (display: string) => ({ kind: 'member', id: 'm', display });

function form(container: HTMLElement, name: string) {
  return container.querySelector(
    `form[aria-label="${name}"]`
  ) as HTMLFormElement;
}

function field(root: HTMLElement, label: string) {
  return [...root.querySelectorAll('label')]
    .find((l) => l.querySelector('span')?.textContent === label)!
    .querySelector('input, textarea, select') as
    | HTMLInputElement
    | HTMLTextAreaElement
    | HTMLSelectElement;
}

function type(
  element: HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement,
  value: string
) {
  element.value = value;
  element.dispatchEvent(
    new Event(element instanceof HTMLSelectElement ? 'change' : 'input', {
      bubbles: true,
    })
  );
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

function row(
  identifier: string,
  state: 'mapped' | 'unmapped' | 'not_applicable',
  extra: Record<string, unknown> = {}
) {
  return {
    edition_id: editionId,
    identifier,
    kind: 'criterion',
    category: 'security',
    parent_identifier: null,
    summary: `${identifier} summary.`,
    coverage_state: state,
    mapped_controls: [],
    pending_proposal_count: 0,
    ...extra,
  };
}

function decisionVersion(status: string) {
  return {
    version_number: 1,
    status,
    rationale: 'Not in scope.',
    proposed_by: person('Casey Lead'),
    proposed_at: '2026-09-20T00:00:00Z',
    review_decision_id: status === 'pending' ? null : 'r',
    reviewed_by: status === 'pending' ? null : person('Riley Reviewer'),
    review_rationale: status === 'pending' ? null : 'Agreed.',
    reviewed_at: status === 'pending' ? null : '2026-09-21T00:00:00Z',
    separation_of_duties_waiver_id: null,
    withdrawn_by: null,
    withdrawal_rationale: null,
    withdrawn_at: null,
  };
}

function decisionFor(identifier: string, status: string, revision = 2) {
  return {
    tenant_id: tenantId,
    program_id: programId,
    decision_id: decisionId,
    edition_id: editionId,
    criterion_identifier: identifier,
    revision,
    status,
    active_version_number: status === 'accepted' ? 1 : null,
    versions: [decisionVersion(status)],
  };
}

function answers(rows: unknown[], decisions: unknown[] = []) {
  api.reply(`GET ${programPath}/criteria-coverage`, 200, {
    items: rows,
    next_cursor: null,
  });
  api.reply(`GET ${programPath}/controls`, 200, {
    items: [],
    next_cursor: null,
  });
  api.reply(`GET ${applicability}`, 200, {
    items: decisions,
    next_cursor: null,
  });
}

async function mountCoverage(rows: unknown[], decisions: unknown[] = []) {
  answers(rows, decisions);
  const container = mount(() => <CoverageCard program={program} />);
  await vi.waitFor(() =>
    expect(container.querySelectorAll('.criteria-coverage > li').length).toBe(
      rows.length
    )
  );
  return container;
}

function remapRow() {
  return row('CC6.1', 'mapped', {
    mapped_controls: [
      {
        mapping_id: mappingId,
        control_id: controlId,
        control_version_id: versionId,
        version_number: 1,
        applicability_explanation: 'Production.',
        remap_required: true,
      },
    ],
  });
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

describe('criterion not-applicable and remap flows (R1-06 frontend #536)', () => {
  it('ShouldProposeNotApplicableGivenAnUnmappedCriterion', async () => {
    // Arrange
    api.reply(`POST ${applicability}`, 200, {
      decision_id: decisionId,
      revision: 1,
      version_number: 1,
    });
    const container = await mountCoverage([row('CC6.2', 'unmapped')]);
    const propose = form(container, 'Mark CC6.2 not applicable');
    type(
      field(propose, 'Why this criterion does not apply'),
      'No physical sites.'
    );

    // Act
    submit(propose);
    await vi.waitFor(() => expect(sent('POST', applicability)).toBeDefined());

    // Assert
    expect(sent('POST', applicability)).toEqual({
      edition_id: editionId,
      criterion_identifier: 'CC6.2',
      expected_revision: 0,
      rationale: 'No physical sites.',
    });
    await vi.waitFor(() =>
      expect(container.textContent).toContain('counts only after review')
    );
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldNotOfferNotApplicableGivenAPointOfFocus', async () => {
    // Arrange
    const container = await mountCoverage([
      row('CC6.2-POF1', 'unmapped', { kind: 'point_of_focus' }),
    ]);

    // Assert
    expect(container.querySelector('form')).toBeNull();
  });

  it('ShouldShowNotApplicableDistinctFromAnUnmappedGap', async () => {
    // Arrange
    const container = await mountCoverage(
      [
        row('CC6.2', 'not_applicable', {
          not_applicable_decision_id: decisionId,
        }),
        row('CC6.3', 'unmapped'),
      ],
      [decisionFor('CC6.2', 'accepted')]
    );

    // Assert
    const items = container.querySelectorAll('.criteria-coverage > li');
    expect(items[0].className).toContain('criteria-coverage-not_applicable');
    expect(items[0].textContent).toContain('Not applicable');
    expect(items[0].textContent).toContain('Not in scope.');
    expect(items[0].textContent).not.toContain('Unmapped');
    expect(items[1].textContent).toContain('Unmapped');
    const summary = container.querySelector('[role="status"]')?.textContent;
    expect(summary).toContain('1 are unmapped gaps');
    expect(summary).toContain('1 not applicable');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldReviewAPendingNotApplicableProposal', async () => {
    // Arrange
    api.reply(`POST ${applicability}/${decisionId}/reviews`, 204);
    const container = await mountCoverage(
      [row('CC6.2', 'unmapped')],
      [decisionFor('CC6.2', 'pending')]
    );
    expect(form(container, 'Mark CC6.2 not applicable')).toBeNull();
    const review = form(container, 'Review not-applicable proposal for CC6.2');
    (
      review.querySelectorAll('input[type="radio"]')[0] as HTMLInputElement
    ).click();
    type(field(review, 'Review rationale'), 'Confirmed.');

    // Act
    submit(review);
    await vi.waitFor(() =>
      expect(
        sent('POST', `${applicability}/${decisionId}/reviews`)
      ).toBeDefined()
    );

    // Assert
    expect(sent('POST', `${applicability}/${decisionId}/reviews`)).toEqual({
      expected_revision: 2,
      outcome: 'accept',
      rationale: 'Confirmed.',
    });
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldWithdrawAnAcceptedNotApplicableDecision', async () => {
    // Arrange
    api.reply(`POST ${applicability}/${decisionId}/withdrawals`, 204);
    const container = await mountCoverage(
      [
        row('CC6.2', 'not_applicable', {
          not_applicable_decision_id: decisionId,
        }),
      ],
      [decisionFor('CC6.2', 'accepted', 3)]
    );
    const withdraw = form(
      container,
      'Withdraw not-applicable decision for CC6.2'
    );
    type(field(withdraw, 'Withdrawal rationale'), 'Scope changed.');

    // Act
    submit(withdraw);
    await vi.waitFor(() =>
      expect(
        sent('POST', `${applicability}/${decisionId}/withdrawals`)
      ).toBeDefined()
    );

    // Assert
    expect(sent('POST', `${applicability}/${decisionId}/withdrawals`)).toEqual({
      expected_revision: 3,
      rationale: 'Scope changed.',
    });
  });

  it('ShouldAskToReloadGivenAStaleApplicabilityRevision', async () => {
    // Arrange
    api.reply(
      `POST ${applicability}/${decisionId}/withdrawals`,
      409,
      problem(409, 'Current revision: 4.')
    );
    const container = await mountCoverage(
      [
        row('CC6.2', 'not_applicable', {
          not_applicable_decision_id: decisionId,
        }),
      ],
      [decisionFor('CC6.2', 'accepted', 3)]
    );
    const withdraw = form(
      container,
      'Withdraw not-applicable decision for CC6.2'
    );
    type(field(withdraw, 'Withdrawal rationale'), 'Scope changed.');

    // Act
    submit(withdraw);

    // Assert
    await vi.waitFor(() =>
      expect(container.querySelector('[role="alert"]')?.textContent).toContain(
        'Reload to see their changes'
      )
    );
  });

  it('ShouldKeepCoverageVisibleGivenApplicabilityIsForbidden', async () => {
    // Arrange
    api.reply(`GET ${programPath}/criteria-coverage`, 200, {
      items: [row('CC6.2', 'unmapped')],
      next_cursor: null,
    });
    api.reply(`GET ${programPath}/controls`, 200, {
      items: [],
      next_cursor: null,
    });
    api.reply(`GET ${applicability}`, 403, problem(403, 'Forbidden.'));

    // Act
    const container = mount(() => <CoverageCard program={program} />);
    await vi.waitFor(() =>
      expect(container.textContent).toContain(
        'You do not have permission to view applicability decisions.'
      )
    );

    // Assert
    expect(container.textContent).toContain('CC6.2');
    expect(form(container, 'Mark CC6.2 not applicable')).toBeNull();
  });

  it('ShouldFlagRemapRequiredAndProposeAMappingToTheCurrentControlVersion', async () => {
    // Arrange
    const newVersionId = '0190a1b2-0000-7000-8000-0000000000d2';
    api.reply(`GET ${programPath}/controls/${controlId}/current-version`, 200, {
      tenant_id: tenantId,
      program_id: programId,
      control_id: controlId,
      identifier: 'AC-01',
      version_id: newVersionId,
      revision: 3,
      status: 'approved',
      content_origin: 'organization_authored',
      content: {
        title: 'Access reviews',
        objective: 'o',
        description: 'd',
        implementation_narrative: 'n',
        expected_evidence_descriptions: ['e'],
      },
      effective_from: '2026-09-01',
      predecessor_version_id: versionId,
      owner_assignment_id: 'a',
      owner_member_id: 'o',
      owner_resolution: 'verified_member',
      accepted_review_decision_id: 'x',
      approval_decision_id: 'x',
      approved_by: person('Pat Approver'),
      approval_rationale: 'Ready.',
      approved_at: '2026-09-22T00:00:00Z',
      separation_of_duties_waiver_id: null,
      effective_until: null,
    });
    api.reply(`GET ${programPath}/control-mappings`, 200, {
      items: [
        {
          tenant_id: tenantId,
          program_id: programId,
          mapping_id: mappingId,
          control_id: controlId,
          edition_id: editionId,
          criterion_identifier: 'CC6.1',
          criterion_kind: 'criterion',
          revision: 4,
          status: 'accepted',
          active_version_number: 1,
          active_control_version_id: versionId,
          versions: [
            {
              version_number: 1,
              control_version_id: versionId,
              status: 'accepted',
              rationale: 'Quarterly reviews restrict access.',
              applicability_explanation: 'Production.',
              proposed_by: person('Casey Lead'),
              proposed_at: '2026-09-20T00:00:00Z',
              review_decision_id: 'r',
              reviewed_by: person('Riley Reviewer'),
              review_rationale: 'Agreed.',
              reviewed_at: '2026-09-21T00:00:00Z',
              separation_of_duties_waiver_id: null,
              retired_by: null,
              retirement_rationale: null,
              retired_at: null,
            },
          ],
        },
      ],
      next_cursor: null,
    });
    api.reply(`POST ${programPath}/control-mappings`, 200, {});
    const container = await mountCoverage([remapRow()]);
    expect(container.textContent).toContain('Remap required');
    await vi.waitFor(() =>
      expect(
        form(container, 'Remap CC6.1 to the current control version')
      ).not.toBeNull()
    );
    const remap = form(container, 'Remap CC6.1 to the current control version');
    await vi.waitFor(() =>
      expect(field(remap, 'Why this control addresses it').value).toBe(
        'Quarterly reviews restrict access.'
      )
    );

    // Act
    submit(remap);
    await vi.waitFor(() =>
      expect(sent('POST', `${programPath}/control-mappings`)).toBeDefined()
    );

    // Assert
    expect(sent('POST', `${programPath}/control-mappings`)).toEqual({
      control_id: controlId,
      control_version_id: newVersionId,
      edition_id: editionId,
      criterion_identifier: 'CC6.1',
      expected_revision: 4,
      rationale: 'Quarterly reviews restrict access.',
      applicability_explanation: 'Production.',
    });
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldExplainGivenTheControlHasNoCurrentVersionToRemapTo', async () => {
    // Arrange
    api.reply(
      `GET ${programPath}/controls/${controlId}/current-version`,
      404,
      problem(404, 'The control has no approved version.')
    );
    api.reply(`GET ${programPath}/control-mappings`, 200, {
      items: [],
      next_cursor: null,
    });

    // Act
    const container = await mountCoverage([remapRow()]);

    // Assert
    await vi.waitFor(() =>
      expect(container.textContent).toContain('has no current approved version')
    );
  });
});
