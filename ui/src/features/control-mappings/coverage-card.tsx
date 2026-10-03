import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  Spinner,
  Stack,
} from '@askrjs/themes/components';

import { listControlDrafts } from '../controls/controls.js';
import { categoryLabel } from '../criteria/criteria.js';
import type { Program } from '../programs/programs.js';
import { organizationPath } from '../tenants/tenants.js';
import {
  ProposeNotApplicableForm,
  RemapForm,
  ReviewNotApplicableForm,
  WithdrawNotApplicableForm,
} from './applicability-actions.js';
import {
  activeApplicability,
  listCriteriaCoverage,
  listCriterionApplicability,
  pendingApplicability,
  ProgramRequestError,
  type ApplicabilityDecision,
  type CoverageRow,
} from './mappings.js';

async function loadCoverage(
  programId: string,
  editionId: string,
  filter: string
) {
  const [rows, controls, applicability] = await Promise.all([
    listCriteriaCoverage(
      programId,
      editionId,
      filter === 'mapped' || filter === 'unmapped' ? filter : undefined
    ),
    // Control identifiers make the mapped list readable; coverage still loads if controls are hidden.
    listControlDrafts(programId).catch(() => []),
    // Coverage stays readable when applicability decisions are hidden or unavailable.
    listCriterionApplicability(programId, editionId).then(
      (decisions) => ({ decisions, unavailable: null as string | null }),
      (error: Error) => ({
        decisions: [] as ApplicabilityDecision[],
        unavailable:
          error instanceof ProgramRequestError && error.status === 403
            ? 'You do not have permission to view applicability decisions.'
            : `Applicability decisions could not be loaded: ${error.message}`,
      })
    ),
  ]);
  const decisions = new Map<string, ApplicabilityDecision>();
  for (const decision of applicability.decisions) {
    const known = decisions.get(decision.criterionIdentifier);
    if (
      !known ||
      pendingApplicability(decision) ||
      (decision.activeVersionNumber !== null && !pendingApplicability(known))
    ) {
      decisions.set(decision.criterionIdentifier, decision);
    }
  }
  return {
    decisions,
    applicabilityUnavailable: applicability.unavailable,
    rows:
      filter === 'not_applicable'
        ? rows.filter((row) => row.coverageState === 'not_applicable')
        : rows,
    identifiers: new Map(
      controls.map((control) => [control.controlId, control.identifier])
    ),
  };
}

function coverageLabel(coverageState: string) {
  if (coverageState === 'mapped') return 'Mapped';
  if (coverageState === 'not_applicable') return 'Not applicable';
  return 'Unmapped';
}

function ApplicabilityPanel({
  programId,
  editionId,
  row,
  decision,
  available,
  onChanged,
}: {
  programId: string;
  editionId: string;
  row: CoverageRow;
  decision: ApplicabilityDecision | null;
  available: boolean;
  onChanged: () => void;
}) {
  if (!available) return null;
  if (row.coverageState === 'not_applicable') {
    const accepted = decision ? activeApplicability(decision) : null;
    return (
      <div className="criteria-applicability">
        <p>
          <strong>Not applicable.</strong> This is a reviewed decision, not an
          unresolved gap.
          {accepted
            ? ` Rationale: ${accepted.rationale} Reviewed by ${accepted.reviewedBy ?? 'unknown'}${accepted.reviewedAt ? ` on ${new Date(accepted.reviewedAt).toLocaleDateString()}` : ''}.`
            : ''}
        </p>
        {decision ? (
          <WithdrawNotApplicableForm
            programId={programId}
            decision={decision}
            onChanged={onChanged}
          />
        ) : null}
      </div>
    );
  }
  if (row.coverageState !== 'unmapped' || row.kind !== 'criterion') return null;
  const pending = decision ? pendingApplicability(decision) : null;
  if (decision && pending) {
    return (
      <div className="criteria-applicability">
        <p>
          Proposed not applicable by {pending.proposedBy}: {pending.rationale}{' '}
          Awaiting review; it counts only after review, so this criterion is
          still an unmapped gap.
        </p>
        <ReviewNotApplicableForm
          programId={programId}
          decision={decision}
          onChanged={onChanged}
        />
      </div>
    );
  }
  return (
    <ProposeNotApplicableForm
      programId={programId}
      editionId={editionId}
      criterion={row.identifier}
      existing={decision}
      onChanged={onChanged}
    />
  );
}

export function CoverageCard({ program }: { program: Program }) {
  const [filter, setFilter] = state('');
  const [version, setVersion] = state(0);
  const refresh = () => setVersion(version() + 1);
  const editionId = program.criteriaEditionId;
  const coverage = resource(
    () =>
      editionId
        ? loadCoverage(program.programId, editionId, filter())
        : Promise.resolve(null),
    [program.programId, editionId, filter(), version()]
  );

  let body: unknown;
  if (!editionId) {
    body = (
      <p>
        Select a criteria catalog to see which criteria have reviewed control
        mappings.
      </p>
    );
  } else if (coverage.pending && !coverage.value) {
    body = <Spinner label="Loading criteria coverage" />;
  } else if (coverage.error) {
    body =
      coverage.error instanceof ProgramRequestError &&
      coverage.error.status === 403 ? (
        <p>You do not have permission to view criteria coverage.</p>
      ) : (
        <Stack gap="sm">
          <p role="alert">{coverage.error.message}</p>
          <Button variant="secondary" onPress={() => coverage.refresh()}>
            Try again
          </Button>
        </Stack>
      );
  } else {
    const rows = coverage.value?.rows ?? [];
    const identifiers =
      coverage.value?.identifiers ?? new Map<string, string>();
    const decisions =
      coverage.value?.decisions ?? new Map<string, ApplicabilityDecision>();
    const unavailable = coverage.value?.applicabilityUnavailable ?? null;
    const count = (name: string) =>
      rows.filter((row) => row.coverageState === name).length;
    const mapped = count('mapped');
    const notApplicable = count('not_applicable');
    const remap = rows.filter((row) =>
      row.mappedControls.some((control) => control.remapRequired)
    ).length;
    body = (
      <Stack gap="md">
        <label className="registration-field">
          <span>Show</span>
          <select
            value={filter()}
            onChange={(event: Event) =>
              setFilter((event.target as HTMLSelectElement).value)
            }
          >
            <option value="">All criteria</option>
            <option value="mapped">Mapped only</option>
            <option value="unmapped">Unmapped only</option>
            <option value="not_applicable">Not applicable only</option>
          </select>
        </label>
        {unavailable ? <p role="note">{unavailable}</p> : null}
        {rows.length === 0 ? (
          <p>No criteria match this filter.</p>
        ) : (
          <>
            <p role="status">
              {mapped} of {rows.length} shown have a reviewed mapping;{' '}
              {count('unmapped')} are unmapped gaps
              {notApplicable > 0 ? `; ${notApplicable} not applicable` : ''}
              {remap > 0 ? `; ${remap} need remapping` : ''}.
            </p>
            <ul className="plain-list criteria-coverage">
              {rows.map((row) => (
                <li
                  className={`criteria-coverage-${row.coverageState}${row.kind === 'point_of_focus' ? ' criteria-point' : ''}`}
                >
                  <strong>{row.identifier}</strong>
                  <span className="criteria-meta">
                    {' '}
                    {categoryLabel(row.category)} ·{' '}
                    {coverageLabel(row.coverageState)}
                    {row.pendingProposalCount > 0
                      ? ` · ${row.pendingProposalCount} proposal${row.pendingProposalCount === 1 ? '' : 's'} awaiting review (not counted)`
                      : ''}
                  </span>
                  <p>{row.summary}</p>
                  {row.mappedControls.length > 0 ? (
                    <ul className="plain-list">
                      {row.mappedControls.map((control) => (
                        <li>
                          <a
                            href={organizationPath(
                              `/programs/${program.programId}/controls/${control.controlId}`
                            )}
                          >
                            {identifiers.get(control.controlId) ?? 'Control'}
                          </a>{' '}
                          (mapping version {control.versionNumber}):{' '}
                          {control.applicabilityExplanation}
                          {control.remapRequired ? (
                            <div>
                              <p className="criteria-remap" role="note">
                                <strong>Remap required:</strong> this mapping
                                cites a control version that is no longer the
                                current approved version. It is not moved
                                automatically.
                              </p>
                              <RemapForm
                                programId={program.programId}
                                editionId={editionId}
                                criterion={row.identifier}
                                control={control}
                                onChanged={refresh}
                              />
                            </div>
                          ) : null}
                        </li>
                      ))}
                    </ul>
                  ) : null}
                  <ApplicabilityPanel
                    programId={program.programId}
                    editionId={editionId}
                    row={row}
                    decision={decisions.get(row.identifier) ?? null}
                    available={!unavailable}
                    onChanged={refresh}
                  />
                </li>
              ))}
            </ul>
          </>
        )}
      </Stack>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Criteria coverage</CardTitle>
        <CardDescription>
          Mapped means an independently reviewed control mapping exists. It is
          not a judgment that the criterion is satisfied, and proposals awaiting
          review are not counted.
        </CardDescription>
      </CardHeader>
      <CardContent>{body}</CardContent>
    </Card>
  );
}
