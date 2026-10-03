import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  EmptyState,
  Page,
  PageHeader,
  Spinner,
  Stack,
} from '@askrjs/themes/components';

import { organizationPath } from '../../tenants/tenants.js';
import {
  decideReadiness,
  decisionLabel,
  decisionLabels,
  familyLabel,
  findingOutcomeLabel,
  gapKindLabel,
  getAssessment,
  listAssessments,
  listGaps,
  listOwnerOptions,
  planGap,
  ReadinessRequestError,
  runAssessment,
  type AssessmentSummary,
  type DecisionOutcome,
  type OwnerOption,
  type PlanState,
  type ReadinessAssessment,
  type ReadinessGap,
} from '../readiness.js';

const managementNotice =
  'Readiness is a management decision, never an auditor opinion. A met rule means only that the recorded inputs pass this rule version as of the chosen time; it does not mean a criterion is met by operating controls or that an audit would succeed.';

function inputValue(event: Event) {
  return (event.target as HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement).value;
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString(undefined, { timeZone: 'UTC' });
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString(undefined, { timeZone: 'UTC', timeZoneName: 'short' });
}

// Explains a failure: stale revisions ask for a reload, projection lag asks for a retry, and
// everything else shows the server's reason.
function ActionError({ error }: { error: Error | null }) {
  if (!error) return null;
  const stale =
    error instanceof ReadinessRequestError && error.status === 409 && !error.transient && /stale|revision/i.test(error.message);
  return (
    <p role="alert">
      {stale
        ? `Someone else changed this program's readiness since you opened it. Reload to see their changes, then try again. (${error.message})`
        : error.message}
    </p>
  );
}

function useAction(onDone: () => void) {
  const [pending, setPending] = state(false);
  const [error, setError] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);
  async function run(event: Event, work: () => Promise<void>, success: string) {
    event.preventDefault();
    setError(null);
    setNotice(null);
    setPending(true);
    try {
      await work();
      setNotice(success);
      onDone();
    } catch (failure) {
      setError(failure instanceof Error ? failure : new Error('The request failed.'));
    } finally {
      setPending(false);
    }
  }
  return { pending, error, notice, run };
}

function LoadError({ error, retry }: { error: Error; retry: () => void }) {
  return (
    <Stack gap="sm">
      <p role="alert">{error.message}</p>
      <Button variant="secondary" onPress={retry}>
        Try again
      </Button>
    </Stack>
  );
}

function RunForm({
  programId,
  expectedRevision,
  onRan,
}: {
  programId: string;
  expectedRevision: number | null;
  onRan: (assessmentId: string) => void;
}) {
  const [asOf, setAsOf] = state('');
  const action = useAction(() => setAsOf(''));
  return (
    <form
      aria-label="Run readiness assessment"
      onSubmit={(event: Event) =>
        void action.run(
          event,
          async () => {
            const value = asOf().trim();
            const id = await runAssessment(programId, expectedRevision ?? 0, value === '' ? null : new Date(`${value}Z`).toISOString());
            onRan(id);
          },
          'Assessment recorded.'
        )
      }
    >
      <Stack gap="sm">
        <label className="registration-field">
          <span>As of (UTC, optional; leave blank for now)</span>
          <input type="datetime-local" value={asOf()} onInput={(event: Event) => setAsOf(inputValue(event))} />
        </label>
        <ActionError error={action.error()} />
        {action.notice() ? <p role="status">{action.notice()}</p> : null}
        <Button variant="primary" type="submit" disabled={action.pending() || expectedRevision === null}>
          {action.pending() ? 'Running…' : 'Run assessment'}
        </Button>
      </Stack>
    </form>
  );
}

function AssessmentList({
  items,
  selected,
  onSelect,
}: {
  items: AssessmentSummary[];
  selected: string | null;
  onSelect: (id: string) => void;
}) {
  return (
    <table className="readiness-table">
      <caption>Assessments, newest first</caption>
      <thead>
        <tr>
          <th scope="col">As of</th>
          <th scope="col">Rules met</th>
          <th scope="col">Gaps</th>
          <th scope="col">Run by</th>
          <th scope="col">Decision</th>
          <th scope="col">
            <span className="visually-hidden">Actions</span>
          </th>
        </tr>
      </thead>
      <tbody>
        {items.map((item, index) => (
          <tr aria-current={item.assessmentId === selected ? 'true' : undefined}>
            <td>
              {formatDateTime(item.asOf)}
              {index === 0 ? ' (latest)' : ''}
            </td>
            <td>{item.ruleMetCount}</td>
            <td>{item.gapCount}</td>
            <td>
              {item.runBy}, {formatDate(item.runAt)}
            </td>
            <td>{item.decisionOutcome ? decisionLabel(item.decisionOutcome) : 'Not decided'}</td>
            <td>
              <Button variant="secondary" onPress={() => onSelect(item.assessmentId)}>
                View assessment as of {formatDateTime(item.asOf)}
              </Button>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function AssessmentSummaryCard({ assessment }: { assessment: ReadinessAssessment }) {
  const notAssessed = assessment.inputs.filter((input) => input.status === 'not_assessed');
  return (
    <Card>
      <CardHeader>
        <CardTitle>Assessment as of {formatDateTime(assessment.asOf)}</CardTitle>
        <CardDescription>
          {assessment.ruleMetCount} rules met and {assessment.gapCount} gaps under {assessment.ruleVersion}. Run by {assessment.runBy} on{' '}
          {formatDateTime(assessment.runAt)}.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Stack gap="sm">
          <p className="readiness-notice">{managementNotice}</p>
          {notAssessed.length > 0 ? (
            <p>
              <strong>Inputs not assessed:</strong> {notAssessed.map((input) => familyLabel(input.family)).join(', ')}. Each is listed as a gap,
              never counted as a positive.
            </p>
          ) : null}
          <table className="readiness-table">
            <caption>Inputs</caption>
            <thead>
              <tr>
                <th scope="col">Source</th>
                <th scope="col">Status</th>
                <th scope="col">Records</th>
                <th scope="col">Explanation</th>
              </tr>
            </thead>
            <tbody>
              {assessment.inputs.map((input) => (
                <tr>
                  <td>{familyLabel(input.family)}</td>
                  <td>{input.status === 'not_assessed' ? 'Not assessed' : input.status.replaceAll('_', ' ')}</td>
                  <td>{input.recordCount}</td>
                  <td>{input.explanation}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="readiness-fingerprint">Input fingerprint: {assessment.inputFingerprint}</p>
        </Stack>
      </CardContent>
    </Card>
  );
}

function FindingsCard({ assessment }: { assessment: ReadinessAssessment }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Per-criterion findings</CardTitle>
        <CardDescription>What each rule found in the recorded inputs. A finding is not a conclusion about the criterion.</CardDescription>
      </CardHeader>
      <CardContent>
        {assessment.findings.length === 0 ? (
          <p>No criterion findings were recorded for this assessment.</p>
        ) : (
          <table className="readiness-table">
            <caption>Findings</caption>
            <thead>
              <tr>
                <th scope="col">Criterion</th>
                <th scope="col">Rule</th>
                <th scope="col">Outcome</th>
                <th scope="col">Explanation</th>
              </tr>
            </thead>
            <tbody>
              {assessment.findings.map((finding) => (
                <tr>
                  <td>
                    <strong>{finding.criterionIdentifier}</strong> {finding.summary}
                  </td>
                  <td>{finding.ruleId.replaceAll('_', ' ')}</td>
                  <td>
                    <span className={`readiness-outcome readiness-outcome-${finding.outcome === 'rule_met' ? 'met' : 'gap'}`}>
                      {findingOutcomeLabel(finding.outcome)}
                    </span>
                  </td>
                  <td>{finding.explanation}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </CardContent>
    </Card>
  );
}

function PlanForm({
  programId,
  gap,
  revision,
  owners,
  onSaved,
}: {
  programId: string;
  gap: ReadinessGap;
  revision: number;
  owners: OwnerOption[];
  onSaved: () => void;
}) {
  const [owner, setOwner] = state(gap.plan?.ownerMemberId ?? owners[0]?.memberId ?? '');
  const [targetDate, setTargetDate] = state(gap.plan?.targetDate ?? '');
  const [planAction, setPlanAction] = state(gap.plan?.action ?? '');
  const action = useAction(onSaved);
  return (
    <form
      aria-label={`Plan gap ${gap.subject}`}
      onSubmit={(event: Event) =>
        void action.run(
          event,
          () => planGap(programId, gap.gapId, revision, { ownerMemberId: owner(), targetDate: targetDate(), action: planAction().trim() }),
          'Gap plan saved.'
        )
      }
    >
      <Stack gap="sm">
        <label className="registration-field">
          <span>Owner</span>
          <select value={owner()} required onChange={(event: Event) => setOwner(inputValue(event))}>
            {owners.map((option) => (
              <option value={option.memberId} selected={owner() === option.memberId}>
                {option.label}
              </option>
            ))}
          </select>
        </label>
        <label className="registration-field">
          <span>Target date (today or later)</span>
          <input type="date" value={targetDate()} required onInput={(event: Event) => setTargetDate(inputValue(event))} />
        </label>
        <label className="registration-field">
          <span>Action</span>
          <textarea value={planAction()} required maxLength={4000} onInput={(event: Event) => setPlanAction(inputValue(event))} />
        </label>
        <ActionError error={action.error()} />
        {action.notice() ? <p role="status">{action.notice()}</p> : null}
        <Button variant="primary" type="submit" disabled={action.pending() || owners.length === 0}>
          {action.pending() ? 'Saving…' : gap.plan ? 'Update plan' : 'Save plan'}
        </Button>
      </Stack>
    </form>
  );
}

function GapPlanCard({
  programId,
  assessment,
  onChanged,
}: {
  programId: string;
  assessment: ReadinessAssessment;
  onChanged: () => void;
}) {
  const [planState, setPlanState] = state<PlanState>('all');
  const gaps = resource(() => listGaps(programId, assessment.assessmentId, planState()), [programId, assessment.assessmentId, assessment.revision, planState()]);
  const owners = resource(() => listOwnerOptions(), []);
  const ownerLabel = (memberId: string) => owners.value?.find((o) => o.memberId === memberId)?.label ?? `member ${memberId.slice(0, 8)}`;
  const unplanned = assessment.gaps.filter((gap) => gap.plan === null).length;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Gap plan</CardTitle>
        <CardDescription>
          {assessment.gapCount === 0
            ? 'This assessment recorded no gaps.'
            : `${assessment.gapCount} gaps, ${unplanned} without an owner and target date. Plans follow a gap across reassessments.`}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Stack gap="sm">
          <label className="registration-field">
            <span>Show gaps</span>
            <select value={planState()} onChange={(event: Event) => setPlanState(inputValue(event) as PlanState)}>
              <option value="all" selected={planState() === 'all'}>
                All gaps
              </option>
              <option value="unplanned" selected={planState() === 'unplanned'}>
                Unplanned
              </option>
              <option value="planned" selected={planState() === 'planned'}>
                Planned
              </option>
            </select>
          </label>
          {owners.error ? <p role="alert">Gap owners could not be loaded: {owners.error.message}</p> : null}
          {gaps.pending && !gaps.value ? (
            <Spinner label="Loading gaps" />
          ) : gaps.error ? (
            <LoadError error={gaps.error} retry={() => gaps.refresh()} />
          ) : (gaps.value ?? []).length === 0 ? (
            <p>No gaps match this filter.</p>
          ) : (
            <ul className="plain-list readiness-gaps">
              {(gaps.value ?? []).map((gap) => (
                <li className="readiness-gap">
                  <h3>
                    {gapKindLabel(gap.kind)}: {gap.kind === 'input_not_assessed' ? familyLabel(gap.subject) : gap.subject}
                  </h3>
                  <p>{gap.explanation}</p>
                  {gap.plan ? (
                    <p>
                      <strong>Plan:</strong> {ownerLabel(gap.plan.ownerMemberId)} by {formatDate(gap.plan.targetDate)}. {gap.plan.action} (planned by{' '}
                      {gap.plan.plannedBy}, {formatDate(gap.plan.plannedAt)})
                    </p>
                  ) : (
                    <p>
                      <strong>Not planned.</strong> Assign an owner and target date.
                    </p>
                  )}
                  <details>
                    <summary>
                      {gap.plan ? 'Change plan' : 'Plan'} for {gap.subject}
                    </summary>
                    {owners.pending && !owners.value ? (
                      <Spinner label="Loading owners" />
                    ) : (
                      <PlanForm programId={programId} gap={gap} revision={assessment.revision} owners={owners.value ?? []} onSaved={onChanged} />
                    )}
                  </details>
                </li>
              ))}
            </ul>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
}

function DecisionCard({
  programId,
  assessment,
  isLatest,
  onDecided,
}: {
  programId: string;
  assessment: ReadinessAssessment;
  isLatest: boolean;
  onDecided: () => void;
}) {
  const [outcome, setOutcome] = state<DecisionOutcome>('do_not_proceed');
  const [rationale, setRationale] = state('');
  const [waiver, setWaiver] = state('');
  const action = useAction(onDecided);
  const unplanned = assessment.gaps.filter((gap) => gap.plan === null).length;
  const blocked = outcome() === 'proceed' && unplanned > 0;
  const decision = assessment.decision;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Management decision</CardTitle>
        <CardDescription>{managementNotice}</CardDescription>
      </CardHeader>
      <CardContent>
        {decision ? (
          <p>
            <strong>{decisionLabel(decision.outcome)}</strong>, decided by {decision.decidedBy} on {formatDateTime(decision.decidedAt)}
            {decision.waiverId ? ' under a separation-of-duties waiver' : ''}. {decision.rationale}
          </p>
        ) : !isLatest ? (
          <p>Only the latest assessment can be decided. Open the latest assessment to record a decision.</p>
        ) : (
          <form
            aria-label="Record readiness decision"
            onSubmit={(event: Event) =>
              void action.run(
                event,
                () =>
                  decideReadiness(programId, assessment.assessmentId, assessment.revision, {
                    outcome: outcome(),
                    rationale: rationale().trim(),
                    waiverId: waiver().trim() === '' ? null : waiver().trim(),
                  }),
                'Decision recorded.'
              )
            }
          >
            <Stack gap="sm">
              <p>
                Separation of duties: the member who ran this assessment ({assessment.runBy}) cannot decide it unless an approved separation-of-duties
                waiver covers this assessment and revision. Proceed also needs every gap to have an owner and a target date.
              </p>
              <fieldset className="readiness-outcomes">
                <legend>Decision</legend>
                {(Object.keys(decisionLabels) as DecisionOutcome[]).map((key) => (
                  <label>
                    <input
                      type="radio"
                      name="readiness-outcome"
                      value={key}
                      checked={outcome() === key}
                      onChange={() => setOutcome(key)}
                    />{' '}
                    {decisionLabels[key]}
                  </label>
                ))}
              </fieldset>
              {blocked ? (
                <p role="status">
                  {unplanned} {unplanned === 1 ? 'gap has' : 'gaps have'} no owner and target date. Plan every gap before deciding to proceed.
                </p>
              ) : null}
              <label className="registration-field">
                <span>Rationale</span>
                <textarea value={rationale()} required maxLength={4000} onInput={(event: Event) => setRationale(inputValue(event))} />
              </label>
              <label className="registration-field">
                <span>Separation-of-duties waiver id (only if you ran this assessment)</span>
                <input type="text" value={waiver()} onInput={(event: Event) => setWaiver(inputValue(event))} />
              </label>
              <ActionError error={action.error()} />
              {action.notice() ? <p role="status">{action.notice()}</p> : null}
              <Button variant="primary" type="submit" disabled={action.pending() || blocked}>
                {action.pending() ? 'Recording…' : 'Record decision'}
              </Button>
            </Stack>
          </form>
        )}
      </CardContent>
    </Card>
  );
}

function AssessmentDetail({
  programId,
  assessmentId,
  isLatest,
  version,
  onChanged,
  onRevision,
}: {
  programId: string;
  assessmentId: string;
  isLatest: boolean;
  version: number;
  onChanged: () => void;
  onRevision: (revision: number) => void;
}) {
  const assessment = resource(async () => {
    const value = await getAssessment(programId, assessmentId);
    onRevision(value.revision);
    return value;
  }, [programId, assessmentId, version]);

  if (assessment.pending && !assessment.value) return <Spinner label="Loading assessment" />;
  if (assessment.error) {
    const missing = assessment.error instanceof ReadinessRequestError && assessment.error.status === 404;
    return missing ? (
      <p role="alert">This assessment was not found.</p>
    ) : (
      <LoadError error={assessment.error} retry={() => assessment.refresh()} />
    );
  }
  const current = assessment.value!;
  return (
    <Stack gap="md">
      <AssessmentSummaryCard assessment={current} />
      <FindingsCard assessment={current} />
      <GapPlanCard programId={programId} assessment={current} onChanged={onChanged} />
      <DecisionCard programId={programId} assessment={current} isLatest={isLatest} onDecided={onChanged} />
    </Stack>
  );
}

export function ProgramReadinessPage({ programId }: { programId: string }) {
  const [version, setVersion] = state(0);
  const [selected, setSelected] = state<string | null>(null);
  const [revision, setRevision] = state<number | null>(null);
  const assessments = resource(() => listAssessments(programId), [programId, version()]);
  const back = <a href={organizationPath(`/programs/${programId}`)}>Back to program</a>;
  const reload = () => setVersion(version() + 1);

  if (assessments.error instanceof ReadinessRequestError && assessments.error.status === 403) {
    return (
      <Page>
        <EmptyState
          title="Readiness is not available to you"
          titleAs="h1"
          description="Readiness assessments need the program manage permission. Ask a Compliance Lead or Org Admin for access."
          action={back}
        />
      </Page>
    );
  }
  if (assessments.error instanceof ReadinessRequestError && assessments.error.status === 404) {
    return (
      <Page>
        <EmptyState title="Program not found" titleAs="h1" description="This program does not exist or was removed." action={back} />
      </Page>
    );
  }

  const items = assessments.value ?? [];
  const latestId = items[0]?.assessmentId ?? null;
  const shown = selected() ?? latestId;
  // Before the first assessment the ledger is at revision zero; afterwards the detail view reports it.
  const expectedRevision = assessments.value === undefined ? null : latestId === null ? 0 : revision();

  return (
    <Page>
      <PageHeader title="Readiness" description="Assess whether this program is ready to start its audit window, own the gap plan, and record management's decision." />
      <Stack gap="md">
        {back}
        <p className="readiness-notice">{managementNotice}</p>
        <Card>
          <CardHeader>
            <CardTitle>Run an assessment</CardTitle>
            <CardDescription>Evaluates the recorded criteria, mappings, and controls as of a point in time. Assessments are immutable once recorded.</CardDescription>
          </CardHeader>
          <CardContent>
            <RunForm
              programId={programId}
              expectedRevision={expectedRevision}
              onRan={(id) => {
                setSelected(id);
                reload();
              }}
            />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Assessments</CardTitle>
          </CardHeader>
          <CardContent>
            {assessments.pending && !assessments.value ? (
              <Spinner label="Loading assessments" />
            ) : assessments.error ? (
              <LoadError error={assessments.error} retry={() => assessments.refresh()} />
            ) : items.length === 0 ? (
              <p>No readiness assessments yet. Run the first assessment above.</p>
            ) : (
              <AssessmentList items={items} selected={shown} onSelect={setSelected} />
            )}
          </CardContent>
        </Card>
        {shown ? (
          <AssessmentDetail
            programId={programId}
            assessmentId={shown}
            isLatest={shown === latestId}
            version={version()}
            onChanged={reload}
            onRevision={setRevision}
          />
        ) : null}
      </Stack>
    </Page>
  );
}
