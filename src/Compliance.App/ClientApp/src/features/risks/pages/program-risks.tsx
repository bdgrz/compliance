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
  acceptRisk,
  authorityLabels,
  chooseRiskTreatment,
  createRiskDraft,
  getRiskEvaluation,
  getRiskMethod,
  listRiskDrafts,
  listRiskEvaluationHistory,
  phaseLabels,
  publishRiskMethod,
  recordRiskAssessment,
  RiskRequestError,
  riskStatusLabel,
  treatmentLabels,
  type ApproverAuthority,
  type AssessmentPhase,
  type RiskAssessment,
  type RiskDraft,
  type RiskEvaluation,
  type RiskMethod,
  type TreatmentKind,
} from '../risks.js';

const levels = [1, 2, 3, 4, 5];

function inputValue(event: Event) {
  return (event.target as HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement).value;
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString(undefined, { timeZone: 'UTC' });
}

// Explains an action failure: stale revisions ask for a reload, projection lag asks for a retry,
// and everything else shows the server's reason.
function ActionError({ error, record }: { error: Error | null; record: string }) {
  if (!error) return null;
  const conflict = error instanceof RiskRequestError && error.status === 409 && !error.transient;
  return (
    <p role="alert">
      {conflict
        ? `Someone else changed this ${record} since you opened it. Reload to see their changes, then try again. (${error.message})`
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

function MethodEditor({
  programId,
  method,
  onSaved,
}: {
  programId: string;
  method: RiskMethod | null;
  onSaved: () => void;
}) {
  const [likelihood, setLikelihood] = state<string[]>(method?.likelihoodScale ?? ['Rare', 'Unlikely', 'Possible', 'Likely', 'Almost certain']);
  const [impact, setImpact] = state<string[]>(method?.impactScale ?? ['Negligible', 'Minor', 'Moderate', 'Major', 'Severe']);
  const [appetite, setAppetite] = state(method?.appetiteThreshold?.toString() ?? '');
  const action = useAction(onSaved);

  function scaleInputs(label: string, values: string[], set: (next: string[]) => void) {
    return (
      <fieldset className="risk-scale">
        <legend>{label}</legend>
        {values.map((value, index) => (
          <label className="registration-field">
            <span>
              {label} {index + 1}
            </span>
            <input
              type="text"
              value={value}
              required
              onInput={(event: Event) => set(values.map((v, i) => (i === index ? inputValue(event) : v)))}
            />
          </label>
        ))}
      </fieldset>
    );
  }

  return (
    <form
      onSubmit={(event: Event) =>
        void action.run(
          event,
          () =>
            publishRiskMethod(
              programId,
              method?.version ?? 0,
              likelihood().map((v) => v.trim()),
              impact().map((v) => v.trim()),
              appetite().trim() === '' ? null : Number(appetite())
            ),
          'Risk method published.'
        )
      }
    >
      <Stack gap="sm">
        {scaleInputs('Likelihood', likelihood(), setLikelihood)}
        {scaleInputs('Impact', impact(), setImpact)}
        <label className="registration-field">
          <span>Risk appetite threshold (score 1–25, optional)</span>
          <input type="number" value={appetite()} onInput={(event: Event) => setAppetite(inputValue(event))} />
        </label>
        <ActionError error={action.error()} record="risk method" />
        {action.notice() ? <p role="status">{action.notice()}</p> : null}
        <Button variant="primary" type="submit" disabled={action.pending()}>
          {action.pending() ? 'Publishing…' : method ? `Publish version ${method.version + 1}` : 'Publish risk method'}
        </Button>
      </Stack>
    </form>
  );
}

function MethodCard({ programId, method, onSaved }: { programId: string; method: RiskMethod | null; onSaved: () => void }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Risk method</CardTitle>
        <CardDescription>
          {method
            ? `Version ${method.version}, qualitative 5×5. Score = likelihood × impact. Appetite: ${
                method.appetiteThreshold === null ? 'not set' : `score ${method.appetiteThreshold} or below`
              }. Risks are reassessed every year (${method.reassessmentInterval}). Published by ${method.publishedBy} on ${formatDate(method.publishedAt)}.`
            : 'No risk method is published for this program yet. Publish one before assessing risks.'}
        </CardDescription>
      </CardHeader>
      <CardContent>
        {method ? (
          <details>
            <summary>Scales and new version</summary>
            <dl className="risk-scale-summary">
              {levels.map((level) => (
                <div>
                  <dt>{level}</dt>
                  <dd>
                    Likelihood: {method.likelihoodScale[level - 1]} · Impact: {method.impactScale[level - 1]}
                  </dd>
                </div>
              ))}
            </dl>
            <MethodEditor programId={programId} method={method} onSaved={onSaved} />
          </details>
        ) : (
          <MethodEditor programId={programId} method={null} onSaved={onSaved} />
        )}
      </CardContent>
    </Card>
  );
}

function CreateRiskForm({ programId, onCreated }: { programId: string; onCreated: () => void }) {
  const [identifier, setIdentifier] = state('');
  const [title, setTitle] = state('');
  const [scenario, setScenario] = state('');
  const [effect, setEffect] = state('');
  const action = useAction(() => {
    setIdentifier('');
    setTitle('');
    setScenario('');
    setEffect('');
    onCreated();
  });
  return (
    <form
      onSubmit={(event: Event) =>
        void action.run(
          event,
          async () => {
            await createRiskDraft(programId, {
              identifier: identifier().trim(),
              title: title().trim(),
              scenario: scenario().trim(),
              potentialEffect: effect().trim(),
            });
          },
          'Risk recorded.'
        )
      }
    >
      <Stack gap="sm">
        <label className="registration-field">
          <span>Identifier</span>
          <input type="text" value={identifier()} required onInput={(event: Event) => setIdentifier(inputValue(event))} />
        </label>
        <label className="registration-field">
          <span>Title</span>
          <input type="text" value={title()} required onInput={(event: Event) => setTitle(inputValue(event))} />
        </label>
        <label className="registration-field">
          <span>Scenario</span>
          <textarea value={scenario()} required onInput={(event: Event) => setScenario(inputValue(event))} />
        </label>
        <label className="registration-field">
          <span>Potential effect</span>
          <textarea value={effect()} required onInput={(event: Event) => setEffect(inputValue(event))} />
        </label>
        <ActionError error={action.error()} record="risk" />
        {action.notice() ? <p role="status">{action.notice()}</p> : null}
        <Button variant="primary" type="submit" disabled={action.pending()}>
          {action.pending() ? 'Recording…' : 'Record risk'}
        </Button>
      </Stack>
    </form>
  );
}

function latest(evaluation: RiskEvaluation, phase: string): RiskAssessment | undefined {
  return [...evaluation.assessments].reverse().find((a) => a.phase === phase);
}

function AssessForm({
  programId,
  risk,
  method,
  evaluation,
  onSaved,
}: {
  programId: string;
  risk: RiskDraft;
  method: RiskMethod;
  evaluation: RiskEvaluation;
  onSaved: (revision: number) => void;
}) {
  const [phase, setPhase] = state<AssessmentPhase>(latest(evaluation, 'inherent') ? 'target' : 'inherent');
  const [likelihood, setLikelihood] = state('3');
  const [impact, setImpact] = state('3');
  const [rationale, setRationale] = state('');
  const action = useAction(() => {
    setRationale('');
    onSaved(evaluation.revision + 1);
  });
  const score = Number(likelihood()) * Number(impact());
  return (
    <form
      aria-label={`Assess ${risk.identifier}`}
      onSubmit={(event: Event) =>
        void action.run(
          event,
          () =>
            recordRiskAssessment(programId, risk.riskId, evaluation.revision, {
              methodVersion: method.version,
              phase: phase(),
              likelihood: Number(likelihood()),
              impact: Number(impact()),
              rationale: rationale().trim(),
            }),
          'Assessment recorded.'
        )
      }
    >
      <Stack gap="sm">
        <label className="registration-field">
          <span>Phase</span>
          <select value={phase()} onChange={(event: Event) => setPhase(inputValue(event) as AssessmentPhase)}>
            {(Object.keys(phaseLabels) as AssessmentPhase[]).map((key) => (
              <option value={key} selected={phase() === key}>
                {phaseLabels[key]}
              </option>
            ))}
          </select>
        </label>
        <label className="registration-field">
          <span>Likelihood</span>
          <select value={likelihood()} onChange={(event: Event) => setLikelihood(inputValue(event))}>
            {levels.map((level) => (
              <option value={String(level)} selected={likelihood() === String(level)}>
                {level} – {method.likelihoodScale[level - 1]}
              </option>
            ))}
          </select>
        </label>
        <label className="registration-field">
          <span>Impact</span>
          <select value={impact()} onChange={(event: Event) => setImpact(inputValue(event))}>
            {levels.map((level) => (
              <option value={String(level)} selected={impact() === String(level)}>
                {level} – {method.impactScale[level - 1]}
              </option>
            ))}
          </select>
        </label>
        <p className="risk-score-preview">
          Score {score}
          {method.appetiteThreshold === null ? '' : score > method.appetiteThreshold ? ' (above appetite)' : ' (within appetite)'}.
          Target and residual assessments require a current inherent assessment on method version {method.version}.
        </p>
        <label className="registration-field">
          <span>Rationale</span>
          <textarea value={rationale()} required onInput={(event: Event) => setRationale(inputValue(event))} />
        </label>
        <ActionError error={action.error()} record="risk" />
        {action.notice() ? <p role="status">{action.notice()}</p> : null}
        <Button variant="primary" type="submit" disabled={action.pending()}>
          {action.pending() ? 'Recording…' : 'Record assessment'}
        </Button>
      </Stack>
    </form>
  );
}

function TreatmentForm({
  programId,
  risk,
  evaluation,
  onSaved,
}: {
  programId: string;
  risk: RiskDraft;
  evaluation: RiskEvaluation;
  onSaved: (revision: number) => void;
}) {
  const [kind, setKind] = state<TreatmentKind>((evaluation.treatment?.kind as TreatmentKind | undefined) ?? 'mitigate');
  const [rationale, setRationale] = state('');
  const action = useAction(() => {
    setRationale('');
    onSaved(evaluation.revision + 1);
  });
  return (
    <form
      aria-label={`Choose treatment for ${risk.identifier}`}
      onSubmit={(event: Event) =>
        void action.run(
          event,
          () => chooseRiskTreatment(programId, risk.riskId, evaluation.revision, kind(), rationale().trim()),
          'Treatment chosen.'
        )
      }
    >
      <Stack gap="sm">
        <label className="registration-field">
          <span>Treatment</span>
          <select value={kind()} onChange={(event: Event) => setKind(inputValue(event) as TreatmentKind)}>
            {(Object.keys(treatmentLabels) as TreatmentKind[]).map((key) => (
              <option value={key} selected={kind() === key}>
                {treatmentLabels[key]}
              </option>
            ))}
          </select>
        </label>
        <p className="risk-rule">
          Residual assessment is not yet available for Mitigate; it needs control-to-risk treatment links.
        </p>
        <label className="registration-field">
          <span>Treatment rationale</span>
          <textarea value={rationale()} required onInput={(event: Event) => setRationale(inputValue(event))} />
        </label>
        <ActionError error={action.error()} record="risk" />
        {action.notice() ? <p role="status">{action.notice()}</p> : null}
        <Button variant="primary" type="submit" disabled={action.pending()}>
          {action.pending() ? 'Saving…' : 'Choose treatment'}
        </Button>
      </Stack>
    </form>
  );
}

function AcceptForm({
  programId,
  risk,
  method,
  evaluation,
  residual,
  onSaved,
}: {
  programId: string;
  risk: RiskDraft;
  method: RiskMethod;
  evaluation: RiskEvaluation;
  residual: RiskAssessment;
  onSaved: (revision: number) => void;
}) {
  const [authority, setAuthority] = state<ApproverAuthority>('compliance_lead');
  const [expires, setExpires] = state('');
  const [rationale, setRationale] = state('');
  const action = useAction(() => {
    setRationale('');
    onSaved(evaluation.revision + 1);
  });
  const limit = new Date();
  limit.setUTCFullYear(limit.getUTCFullYear() + 1);
  const max = limit.toISOString().slice(0, 10);
  const aboveAppetite = method.appetiteThreshold === null || residual.score > method.appetiteThreshold;
  const leadBlocked = authority() === 'compliance_lead' && aboveAppetite;
  const error = action.error();
  const forbidden = error instanceof RiskRequestError && error.status === 403;
  return (
    <form
      aria-label={`Accept ${risk.identifier}`}
      onSubmit={(event: Event) =>
        void action.run(
          event,
          () =>
            acceptRisk(programId, risk.riskId, evaluation.revision, {
              residualAssessmentId: residual.assessmentId,
              authority: authority(),
              expiresAt: `${expires()}T00:00:00Z`,
              rationale: rationale().trim(),
            }),
          'Your acceptance is recorded.'
        )
      }
    >
      <Stack gap="sm">
        <p className="risk-rule">
          Acceptance is your personal decision about residual score {residual.score} and is recorded under your name. It
          needs the program's risk acceptance grant for the authority you choose, expires within 12 months, and cannot be
          made by the person who assessed the residual risk ({residual.assessor}). A compliance lead may not accept a risk
          above appetite or while appetite is unset.
        </p>
        <label className="registration-field">
          <span>Accept as</span>
          <select value={authority()} onChange={(event: Event) => setAuthority(inputValue(event) as ApproverAuthority)}>
            {(Object.keys(authorityLabels) as ApproverAuthority[]).map((key) => (
              <option value={key} selected={authority() === key}>
                {authorityLabels[key]}
              </option>
            ))}
          </select>
        </label>
        {leadBlocked ? (
          <p className="risk-rule" role="note">
            {method.appetiteThreshold === null
              ? 'Appetite is not set, so only an executive can accept this risk.'
              : `Residual score ${residual.score} is above appetite ${method.appetiteThreshold}, so only an executive can accept this risk.`}
          </p>
        ) : null}
        <label className="registration-field">
          <span>Acceptance expires on (no later than {max})</span>
          <input type="date" value={expires()} required onInput={(event: Event) => setExpires(inputValue(event))} />
        </label>
        <label className="registration-field">
          <span>Acceptance rationale</span>
          <textarea value={rationale()} required onInput={(event: Event) => setRationale(inputValue(event))} />
        </label>
        {forbidden ? (
          <p role="alert">{error.message} Ask an Org Admin to grant it, or choose another authority.</p>
        ) : (
          <ActionError error={error} record="risk" />
        )}
        {action.notice() ? <p role="status">{action.notice()}</p> : null}
        <Button variant="primary" type="submit" disabled={action.pending() || leadBlocked}>
          {action.pending() ? 'Recording…' : 'Accept residual risk'}
        </Button>
      </Stack>
    </form>
  );
}

function RiskHistory({ programId, riskId }: { programId: string; riskId: string }) {
  const history = resource(() => listRiskEvaluationHistory(programId, riskId), [programId, riskId]);
  if (history.pending) return <Spinner label="Loading risk history" />;
  if (history.error) {
    return (
      <Stack gap="sm">
        <p role="alert">{history.error.message}</p>
        <Button variant="secondary" onPress={() => history.refresh()}>
          Try again
        </Button>
      </Stack>
    );
  }
  const events = history.value ?? [];
  if (events.length === 0) return <p>No assessment decisions yet.</p>;
  return (
    <ol className="plain-list risk-history">
      {events.map((item) => (
        <li>
          <strong>Revision {item.revision}</strong> by {item.actor} on {formatDate(item.occurredAt)}: {item.summary}
        </li>
      ))}
    </ol>
  );
}

function RiskCard({ programId, risk, method }: { programId: string; risk: RiskDraft; method: RiskMethod | null }) {
  const [minimum, setMinimum] = state<number | undefined>(undefined);
  const [showHistory, setShowHistory] = state(false);
  const evaluation = resource(() => getRiskEvaluation(programId, risk.riskId, minimum()), [programId, risk.riskId, minimum()]);
  const headingId = `risk-${risk.riskId}`;

  let body: unknown;
  if (evaluation.pending && !evaluation.value) {
    body = <Spinner label={`Loading ${risk.identifier} evaluation`} />;
  } else if (evaluation.error) {
    const error = evaluation.error;
    body = (
      <Stack gap="sm">
        <p role="alert">{error.message}</p>
        {error instanceof RiskRequestError && error.status === 403 ? null : (
          <Button variant="secondary" onPress={() => evaluation.refresh()}>
            Try again
          </Button>
        )}
      </Stack>
    );
  } else {
    const current = evaluation.value!;
    const residual = latest(current, 'residual');
    const saved = (revision: number) => setMinimum(revision);
    body = (
      <Stack gap="sm">
        <p>
          <strong>Status:</strong> {riskStatusLabel(current.status)}
          {current.reassessmentDueAt ? ` · reassess by ${formatDate(current.reassessmentDueAt)}` : ''}
        </p>
        {current.status === 'reassessment_due' ? (
          <p className="risk-rule" role="note">
            An acceptance has expired or an assessment is more than a year old. Reassess this risk.
          </p>
        ) : null}
        {current.assessments.length > 0 ? (
          <table className="inventory-table risk-assessments">
            <caption className="visually-hidden">Assessments for {risk.identifier}</caption>
            <thead>
              <tr>
                <th scope="col">Phase</th>
                <th scope="col">Likelihood</th>
                <th scope="col">Impact</th>
                <th scope="col">Score</th>
                <th scope="col">Rationale</th>
                <th scope="col">Assessor</th>
              </tr>
            </thead>
            <tbody>
              {(['inherent', 'target', 'residual'] as const)
                .map((phase) => latest(current, phase))
                .filter((a) => a !== undefined)
                .map((a) => (
                  <tr>
                    <td>{phaseLabels[a.phase as AssessmentPhase] ?? a.phase}</td>
                    <td>
                      {a.likelihood}
                      {method && a.methodVersion === method.version ? ` – ${method.likelihoodScale[a.likelihood - 1]}` : ''}
                    </td>
                    <td>
                      {a.impact}
                      {method && a.methodVersion === method.version ? ` – ${method.impactScale[a.impact - 1]}` : ''}
                    </td>
                    <td>
                      {a.score}
                      {method?.appetiteThreshold != null
                        ? a.score > method.appetiteThreshold
                          ? ' (above appetite)'
                          : ' (within appetite)'
                        : ''}
                    </td>
                    <td>{a.rationale}</td>
                    <td>
                      {a.assessor}, {formatDate(a.assessedAt)} (method v{a.methodVersion})
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
        ) : null}
        {current.treatment ? (
          <p>
            <strong>Treatment:</strong> {treatmentLabels[current.treatment.kind as TreatmentKind] ?? current.treatment.kind} —{' '}
            {current.treatment.rationale} ({current.treatment.chosenBy}, {formatDate(current.treatment.chosenAt)})
          </p>
        ) : null}
        {current.acceptances.length > 0 ? (
          <ul className="plain-list risk-acceptances">
            {current.acceptances.map((a) => (
              <li>
                <strong>Accepted</strong> by {a.approver} as {authorityLabels[a.approverAuthority as ApproverAuthority] ?? a.approverAuthority} on{' '}
                {formatDate(a.acceptedAt)}, residual score {a.residualScore}; expires {formatDate(a.expiresAt)}
                {a.status ? ` (${a.status.replaceAll('_', ' ')})` : ''}. {a.rationale}
              </li>
            ))}
          </ul>
        ) : null}
        {method ? (
          <details>
            <summary>Assess {risk.identifier}</summary>
            <AssessForm programId={programId} risk={risk} method={method} evaluation={current} onSaved={saved} />
          </details>
        ) : (
          <p>Publish a risk method to assess this risk.</p>
        )}
        {latest(current, 'inherent') ? (
          <details>
            <summary>Choose treatment for {risk.identifier}</summary>
            <TreatmentForm programId={programId} risk={risk} evaluation={current} onSaved={saved} />
          </details>
        ) : null}
        {method && residual ? (
          <details>
            <summary>Accept {risk.identifier} (personal decision)</summary>
            <AcceptForm programId={programId} risk={risk} method={method} evaluation={current} residual={residual} onSaved={saved} />
          </details>
        ) : null}
        <Button variant="secondary" onPress={() => setShowHistory(!showHistory())} aria-expanded={showHistory() ? 'true' : 'false'}>
          {showHistory() ? 'Hide history' : 'Show history'}
        </Button>
        {showHistory() ? <RiskHistory programId={programId} riskId={risk.riskId} /> : null}
      </Stack>
    );
  }

  return (
    <li className="risk-card" aria-labelledby={headingId}>
      <h3 id={headingId}>
        {risk.identifier}: {risk.title}
      </h3>
      <p>
        <strong>Scenario:</strong> {risk.scenario}
      </p>
      <p>
        <strong>Potential effect:</strong> {risk.potentialEffect}
      </p>
      {body}
    </li>
  );
}

export function ProgramRisksPage({ programId }: { programId: string }) {
  const [version, setVersion] = state(0);
  const drafts = resource(() => listRiskDrafts(programId), [programId, version()]);
  const method = resource(() => getRiskMethod(programId), [programId, version()]);
  const back = <a href={organizationPath(`/programs/${programId}`)}>Back to program</a>;
  const reload = () => setVersion(version() + 1);

  if (drafts.error instanceof RiskRequestError && drafts.error.status === 403) {
    return (
      <Page>
        <EmptyState
          title="Risks are not available to you"
          titleAs="h1"
          description="Managing this program's risks needs the program manage permission. Ask a Compliance Lead or Org Admin for access."
          action={back}
        />
      </Page>
    );
  }

  return (
    <Page>
      <PageHeader
        title="Risks"
        description="Identify scoped risks, assess them with the program's method, choose a treatment, and record personal acceptance."
      />
      <Stack gap="md">
        {back}
        {method.pending && !method.value ? (
          <Spinner label="Loading risk method" />
        ) : method.error ? (
          <Stack gap="sm">
            <p role="alert">{method.error.message}</p>
            <Button variant="secondary" onPress={() => method.refresh()}>
              Try again
            </Button>
          </Stack>
        ) : (
          <MethodCard programId={programId} method={method.value ?? null} onSaved={reload} />
        )}
        <Card>
          <CardHeader>
            <CardTitle>Record a risk</CardTitle>
          </CardHeader>
          <CardContent>
            <CreateRiskForm programId={programId} onCreated={reload} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Program risks</CardTitle>
          </CardHeader>
          <CardContent>
            {drafts.pending && !drafts.value ? (
              <Spinner label="Loading risks" />
            ) : drafts.error ? (
              <Stack gap="sm">
                <p role="alert">{drafts.error.message}</p>
                <Button variant="secondary" onPress={() => drafts.refresh()}>
                  Try again
                </Button>
              </Stack>
            ) : (drafts.value ?? []).length === 0 ? (
              <p>No risks recorded yet. Record the first risk above.</p>
            ) : (
              <ul className="plain-list risk-list">
                {(drafts.value ?? []).map((risk) => (
                  <RiskCard programId={programId} risk={risk} method={method.value ?? null} />
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </Stack>
    </Page>
  );
}
