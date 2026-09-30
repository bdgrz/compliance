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
import { listCriteriaCoverage, ProgramRequestError } from './mappings.js';

async function loadCoverage(
  programId: string,
  editionId: string,
  filter: string
) {
  const [rows, controls] = await Promise.all([
    listCriteriaCoverage(
      programId,
      editionId,
      filter === 'mapped' || filter === 'unmapped' ? filter : undefined
    ),
    // Control identifiers make the mapped list readable; coverage still loads if controls are hidden.
    listControlDrafts(programId).catch(() => []),
  ]);
  return {
    rows,
    identifiers: new Map(
      controls.map((control) => [control.controlId, control.identifier])
    ),
  };
}

export function CoverageCard({ program }: { program: Program }) {
  const [filter, setFilter] = state('');
  const editionId = program.criteriaEditionId;
  const coverage = resource(
    () =>
      editionId
        ? loadCoverage(program.programId, editionId, filter())
        : Promise.resolve(null),
    [program.programId, editionId, filter()]
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
    const mapped = rows.filter((row) => row.coverageState === 'mapped').length;
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
          </select>
        </label>
        {rows.length === 0 ? (
          <p>No criteria match this filter.</p>
        ) : (
          <>
            <p role="status">
              {mapped} of {rows.length} shown have a reviewed mapping;{' '}
              {rows.length - mapped} are unmapped gaps.
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
                    {row.coverageState === 'mapped' ? 'Mapped' : 'Unmapped'}
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
                        </li>
                      ))}
                    </ul>
                  ) : null}
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
