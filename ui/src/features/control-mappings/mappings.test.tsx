// @vitest-environment jsdom
import { render, type RenderResult } from '@askrjs/askr/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessibilityViolations } from '../../accessibility/axe.js';
import { stubApi } from '../../test-support/api-stub.js';
import type { Program } from '../programs/programs.js';
import { clearActiveTenant, resolveTenantRoute } from '../tenants/tenants.js';
import { CoverageCard } from './coverage-card.js';
import { ControlMappingsCard } from './mappings-card.js';

const tenantId = '0190a1b2-0000-7000-8000-000000000001';
const programId = '0190a1b2-0000-7000-8000-0000000000b1';
const controlId = '0190a1b2-0000-7000-8000-0000000000c1';
const versionId = '0190a1b2-0000-7000-8000-0000000000d1';
const editionId = '0190a1b2-0000-7000-8000-0000000000e9';
const mappingId = '0190a1b2-0000-7000-8000-0000000000m1';
const base = `/api/v1/tenants/${tenantId}`;
const programPath = `${base}/programs/${programId}`;
const mappings = `${programPath}/control-mappings`;
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

function mappingVersion(versionNumber: number, status: string) {
  return {
    version_number: versionNumber,
    control_version_id: versionId,
    status,
    rationale: 'Quarterly reviews restrict access.',
    applicability_explanation: 'Production systems in the boundary.',
    proposed_by: person('Casey Lead'),
    proposed_at: '2026-09-20T00:00:00Z',
    review_decision_id: status === 'pending' ? null : 'r',
    reviewed_by: person('Riley Reviewer'),
    review_rationale: status === 'pending' ? null : 'Agreed.',
    reviewed_at: status === 'pending' ? null : '2026-09-21T00:00:00Z',
    separation_of_duties_waiver_id: null,
    retired_by: person(''),
    retirement_rationale: null,
    retired_at: null,
  };
}

function mapping(status: string, versions: unknown[], active: number | null) {
  return {
    tenant_id: tenantId,
    program_id: programId,
    mapping_id: mappingId,
    control_id: controlId,
    edition_id: editionId,
    criterion_identifier: 'CC6.1',
    criterion_kind: 'criterion',
    revision: 4,
    status,
    active_version_number: active,
    active_control_version_id: active === null ? null : versionId,
    versions,
  };
}

function answers(items: unknown[]) {
  api.reply(`GET ${programPath}`, 200, {
    tenant_id: tenantId,
    program_id: programId,
    name: 'SOC 2 2026',
    stage: 'readiness',
    next_stage: 'type_i',
    revision: 3,
    plan: program.plan,
    last_changed_by_member_id: 'm',
    last_changed_by_display: 'Casey Lead',
    last_changed_at: '2026-09-20T00:00:00Z',
    stage_plan: [],
    criteria_edition_id: editionId,
  });
  api.reply(`GET ${mappings}`, 200, { items, next_cursor: null });
  api.reply(`GET ${base}/criteria-editions/${editionId}/entries`, 200, {
    items: [
      {
        edition_id: editionId,
        identifier: 'CC6.1',
        source_identifier: 'CC6.1',
        category: 'security',
        kind: 'criterion',
        parent_identifier: null,
        summary: 'Logical access security.',
      },
      {
        edition_id: editionId,
        identifier: 'CC6.2',
        source_identifier: 'CC6.2',
        category: 'security',
        kind: 'criterion',
        parent_identifier: null,
        summary: 'User registration.',
      },
    ],
    next_cursor: null,
  });
}

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

describe('control criteria mappings (R1-06 frontend #205)', () => {
  it('ShouldProposeAMappingToACriterionOfTheSelectedEdition', async () => {
    // Arrange
    answers([]);
    api.reply(`POST ${mappings}`, 200, {
      mapping_id: mappingId,
      revision: 1,
      version_number: 1,
    });
    const container = mount(() => (
      <ControlMappingsCard
        programId={programId}
        controlId={controlId}
        currentVersionId={versionId}
      />
    ));
    await vi.waitFor(() =>
      expect(form(container, 'Propose criteria mapping')).not.toBeNull()
    );
    await vi.waitFor(() =>
      expect(container.querySelectorAll('option')).toHaveLength(3)
    );
    const propose = form(container, 'Propose criteria mapping');
    type(field(propose, 'Criterion'), 'CC6.2');
    type(
      field(propose, 'Why this control addresses it'),
      'Access requests are approved.'
    );
    type(
      field(propose, 'Applicability explanation'),
      'Workforce accounts in production.'
    );

    // Act
    submit(propose);
    await vi.waitFor(() => expect(sent('POST', mappings)).toBeDefined());

    // Assert
    expect(sent('POST', mappings)).toEqual({
      control_id: controlId,
      control_version_id: versionId,
      edition_id: editionId,
      criterion_identifier: 'CC6.2',
      expected_revision: 0,
      rationale: 'Access requests are approved.',
      applicability_explanation: 'Workforce accounts in production.',
    });
    expect(container.textContent).toContain('not mapped to any criteria yet');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldReviewAPendingMappingProposal', async () => {
    // Arrange
    answers([mapping('pending', [mappingVersion(1, 'pending')], null)]);
    api.reply(`POST ${mappings}/${mappingId}/reviews`, 204);
    const container = mount(() => (
      <ControlMappingsCard
        programId={programId}
        controlId={controlId}
        currentVersionId={versionId}
      />
    ));
    await vi.waitFor(() =>
      expect(form(container, 'Review mapping to CC6.1')).not.toBeNull()
    );
    const review = form(container, 'Review mapping to CC6.1');
    (
      review.querySelectorAll('input[type="radio"]')[1] as HTMLInputElement
    ).click();
    type(field(review, 'Review rationale'), 'Does not address registration.');

    // Act
    submit(review);
    await vi.waitFor(() =>
      expect(sent('POST', `${mappings}/${mappingId}/reviews`)).toBeDefined()
    );

    // Assert
    expect(sent('POST', `${mappings}/${mappingId}/reviews`)).toEqual({
      expected_revision: 4,
      outcome: 'reject',
      rationale: 'Does not address registration.',
    });
    expect(container.textContent).toContain('no reviewed version');
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldRetireAnAcceptedMappingAndKeepItsHistory', async () => {
    // Arrange
    answers([mapping('accepted', [mappingVersion(1, 'accepted')], 1)]);
    api.reply(`POST ${mappings}/${mappingId}/retirements`, 204);
    const container = mount(() => (
      <ControlMappingsCard
        programId={programId}
        controlId={controlId}
        currentVersionId={versionId}
      />
    ));
    await vi.waitFor(() =>
      expect(form(container, 'Retire mapping to CC6.1')).not.toBeNull()
    );
    const retire = form(container, 'Retire mapping to CC6.1');
    type(field(retire, 'Retirement rationale'), 'Moved to CC6.2.');

    // Act
    submit(retire);
    await vi.waitFor(() =>
      expect(sent('POST', `${mappings}/${mappingId}/retirements`)).toBeDefined()
    );

    // Assert
    expect(sent('POST', `${mappings}/${mappingId}/retirements`)).toEqual({
      expected_revision: 4,
      rationale: 'Moved to CC6.2.',
    });
    expect(
      container.querySelector('.control-mapping-history li')?.textContent
    ).toContain('Reviewed by Riley Reviewer');
  });

  it('ShouldAskToReloadGivenAStaleMappingRevision', async () => {
    // Arrange
    answers([mapping('accepted', [mappingVersion(1, 'accepted')], 1)]);
    api.reply(
      `POST ${mappings}/${mappingId}/retirements`,
      409,
      problem(
        409,
        'The control criterion mapping changed. Current revision: 5. Reload it and retry.'
      )
    );
    const container = mount(() => (
      <ControlMappingsCard
        programId={programId}
        controlId={controlId}
        currentVersionId={versionId}
      />
    ));
    await vi.waitFor(() =>
      expect(form(container, 'Retire mapping to CC6.1')).not.toBeNull()
    );
    const retire = form(container, 'Retire mapping to CC6.1');
    type(field(retire, 'Retirement rationale'), 'Moved.');

    // Act
    submit(retire);
    await vi.waitFor(() =>
      expect(retire.querySelector('[role="alert"]')).not.toBeNull()
    );

    // Assert
    expect(retire.querySelector('[role="alert"]')?.textContent).toContain(
      'Someone else changed this mapping'
    );
  });

  it('ShouldRequireAnApprovedVersionBeforeProposing', async () => {
    // Arrange
    answers([]);

    // Act
    const container = mount(() => (
      <ControlMappingsCard
        programId={programId}
        controlId={controlId}
        currentVersionId={null}
      />
    ));
    await vi.waitFor(() =>
      expect(container.textContent).toContain(
        'Approve this control before proposing mappings'
      )
    );

    // Assert
    expect(form(container, 'Propose criteria mapping')).toBeNull();
  });

  it('ShouldShowForbiddenGivenTheViewerCannotReadMappings', async () => {
    // Arrange
    answers([]);
    api.reply(`GET ${mappings}`, 403, problem(403, 'Forbidden'));

    // Act
    const container = mount(() => (
      <ControlMappingsCard
        programId={programId}
        controlId={controlId}
        currentVersionId={versionId}
      />
    ));

    // Assert
    await vi.waitFor(() =>
      expect(container.textContent).toContain(
        'You do not have permission to view criteria mappings.'
      )
    );
  });
});

describe('criteria coverage (R1-06 frontend #205)', () => {
  function coverageRow(
    identifier: string,
    state: 'mapped' | 'unmapped',
    pending: number
  ) {
    return {
      edition_id: editionId,
      identifier,
      kind: 'criterion',
      category: 'security',
      parent_identifier: null,
      summary: `${identifier} summary.`,
      coverage_state: state,
      mapped_controls:
        state === 'mapped'
          ? [
              {
                mapping_id: mappingId,
                control_id: controlId,
                control_version_id: versionId,
                version_number: 1,
                applicability_explanation: 'Production.',
              },
            ]
          : [],
      pending_proposal_count: pending,
    };
  }

  it('ShouldShowMappedAndUnmappedCriteriaWithoutClaimingSatisfaction', async () => {
    // Arrange
    api.reply(`GET ${programPath}/criteria-coverage`, 200, {
      items: [
        coverageRow('CC6.1', 'mapped', 0),
        coverageRow('CC6.2', 'unmapped', 1),
      ],
      next_cursor: null,
    });
    api.reply(`GET ${programPath}/controls`, 200, {
      items: [
        {
          tenant_id: tenantId,
          program_id: programId,
          control_id: controlId,
          identifier: 'AC-01',
          revision: 2,
          status: 'draft',
          owner_resolution: 'unresolved',
          applicability_resolution: 'unresolved',
          content: {
            title: 'Access reviews',
            objective: 'o',
            description: 'd',
            implementation_narrative: 'n',
            expected_evidence_descriptions: ['e'],
          },
          last_changed_by_member_id: 'm',
          last_changed_by_display: 'Casey Lead',
          last_changed_at: '2026-09-20T00:00:00Z',
        },
      ],
      next_cursor: null,
    });

    // Act
    const container = mount(() => <CoverageCard program={program} />);
    await vi.waitFor(() =>
      expect(
        container.querySelectorAll('.criteria-coverage > li')
      ).toHaveLength(2)
    );

    // Assert
    expect(container.textContent).toContain(
      '1 of 2 shown have a reviewed mapping; 1 are unmapped gaps.'
    );
    expect(container.textContent).toContain(
      '1 proposal awaiting review (not counted)'
    );
    expect(
      container.querySelector(
        `a[href="/acme/programs/${programId}/controls/${controlId}"]`
      )?.textContent
    ).toBe('AC-01');
    expect(
      container.querySelector('.criteria-coverage')?.textContent
    ).not.toMatch(/satisf/i);
    expect(await accessibilityViolations(container)).toEqual([]);
  });

  it('ShouldFilterToUnmappedGaps', async () => {
    // Arrange
    api.reply(`GET ${programPath}/criteria-coverage`, 200, {
      items: [coverageRow('CC6.2', 'unmapped', 0)],
      next_cursor: null,
    });
    api.reply(`GET ${programPath}/controls`, 200, {
      items: [],
      next_cursor: null,
    });
    const container = mount(() => <CoverageCard program={program} />);
    await vi.waitFor(() =>
      expect(container.querySelector('select')).not.toBeNull()
    );

    // Act
    type(container.querySelector('select')!, 'unmapped');
    await vi.waitFor(() =>
      expect(
        api.requested.filter((path) => path.endsWith('/criteria-coverage'))
          .length
      ).toBeGreaterThan(1)
    );

    // Assert
    const urls = api.bodies.filter((b) =>
      b.path.endsWith('/criteria-coverage')
    );
    expect(urls.length).toBeGreaterThan(1);
  });

  it('ShouldExplainProjectionLagWithRetry', async () => {
    // Arrange
    api.reply(
      `GET ${programPath}/criteria-coverage`,
      409,
      problem(409, 'Projection is behind.', true)
    );
    api.reply(`GET ${programPath}/controls`, 200, {
      items: [],
      next_cursor: null,
    });

    // Act
    const container = mount(() => <CoverageCard program={program} />);
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

  it('ShouldAskForACatalogGivenNoEditionSelected', async () => {
    // Arrange
    const withoutEdition = { ...program, criteriaEditionId: null };

    // Act
    const container = mount(() => <CoverageCard program={withoutEdition} />);

    // Assert
    await vi.waitFor(() =>
      expect(container.textContent).toContain('Select a criteria catalog')
    );
  });
});
