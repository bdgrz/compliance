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

import { CriteriaCard } from '../../criteria/criteria-card.js';
import { organizationPath } from '../../tenants/tenants.js';
import {
  getProgram,
  getSetupWork,
  listProgramRevisions,
  ProgramRequestError,
  reviseProgram,
  stageLabel,
  type Program,
  type ProgramPlan,
} from '../programs.js';

const planFields: { key: keyof ProgramPlan; label: string; type: 'date' | 'text' }[] = [
  { key: 'target_readiness_date', label: 'Target readiness date', type: 'date' },
  { key: 'target_type_i_as_of_date', label: 'Target Type I as-of date', type: 'date' },
  { key: 'target_type_ii_start_date', label: 'Target Type II period start', type: 'date' },
  { key: 'target_type_ii_end_date', label: 'Target Type II period end', type: 'date' },
  { key: 'readiness_advisor', label: 'Readiness advisor', type: 'text' },
  { key: 'audit_firm', label: 'Audit firm', type: 'text' },
];

function formatValue(value: string | null, type: 'date' | 'text') {
  if (!value) return 'Not set';
  return type === 'date' ? new Date(`${value}T00:00:00Z`).toLocaleDateString(undefined, { timeZone: 'UTC' }) : value;
}

function PlanEditor({ program, onSaved }: { program: Program; onSaved: () => void }) {
  const [name, setName] = state(program.name);
  const [plan, setPlan] = state<ProgramPlan>({ ...program.plan });
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<ProgramRequestError | Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  async function save(event: Event) {
    event.preventDefault();
    setActionError(null);
    setNotice(null);
    setPending(true);
    try {
      const cleaned = Object.fromEntries(
        Object.entries(plan()).map(([key, value]) => [key, typeof value === 'string' && value.trim() !== '' ? value.trim() : null])
      ) as unknown as ProgramPlan;
      await reviseProgram(program.programId, program.revision, name().trim(), cleaned);
      setNotice('Program plan saved.');
      onSaved();
    } catch (failure) {
      setActionError(failure instanceof Error ? failure : new Error('Unable to save the program.'));
    } finally {
      setPending(false);
    }
  }

  const error = actionError();
  const stale = error instanceof ProgramRequestError && error.status === 409 && !error.transient;

  return (
    <form onSubmit={(event: Event) => void save(event)}>
      <Stack gap="sm">
        <label className="registration-field">
          <span>Program name</span>
          <input
            type="text"
            value={name()}
            onInput={(event: Event) => setName((event.target as HTMLInputElement).value)}
            required
          />
        </label>
        {planFields.map((field) => (
          <label className="registration-field">
            <span>{field.label}</span>
            <input
              type={field.type}
              value={plan()[field.key] ?? ''}
              onInput={(event: Event) =>
                setPlan({ ...plan(), [field.key]: (event.target as HTMLInputElement).value })
              }
            />
          </label>
        ))}
        {error ? (
          <p role="alert">
            {stale
              ? 'Someone else changed this program since you opened it. Reload to see their changes, then edit again.'
              : error.message}
          </p>
        ) : null}
        {notice() ? <p role="status">{notice()}</p> : null}
        <Button variant="primary" type="submit" disabled={pending()}>
          {pending() ? 'Saving…' : 'Save plan'}
        </Button>
      </Stack>
    </form>
  );
}

export function ProgramDetailPage({ programId }: { programId: string }) {
  const [version, setVersion] = state(0);
  const program = resource(() => getProgram(programId), [programId, version()]);
  const setupWork = resource(() => getSetupWork(programId), [programId, version()]);
  const history = resource(() => listProgramRevisions(programId), [programId, version()]);
  const back = <a href={organizationPath('/programs')}>Back to programs</a>;

  if (program.pending && !program.value) {
    return (
      <Page>
        <Spinner label="Loading program" />
      </Page>
    );
  }

  if (program.error) {
    const error = program.error;
    const status = error instanceof ProgramRequestError ? error.status : null;
    return (
      <Page>
        <EmptyState
          title={status === 403 ? 'This program is not available to you' : status === 404 ? 'Program not found' : 'Program could not be loaded'}
          titleAs="h1"
          description={error.message}
          action={
            status === 403 || status === 404 ? (
              back
            ) : (
              <Button variant="primary" onPress={() => program.refresh()}>
                Try again
              </Button>
            )
          }
        />
      </Page>
    );
  }

  const current = program.value!;
  const work = setupWork.value ?? [];

  return (
    <Page>
      <PageHeader
        title={current.name}
        description={`Current stage: ${stageLabel(current.stage)}${current.nextStage ? ` · next: ${stageLabel(current.nextStage)}` : ''}`}
      />
      <Stack gap="md">
        {back}
        <a href={organizationPath(`/programs/${programId}/risks`)}>Risks for this program</a>
        <a href={organizationPath(`/programs/${programId}/controls`)}>Controls for this program</a>
        <a href={organizationPath(`/programs/${programId}/boundaries`)}>System boundaries for this program</a>
        <Card>
          <CardHeader>
            <CardTitle>Setup work before readiness assessment</CardTitle>
            <CardDescription>Decisions and records this program still needs.</CardDescription>
          </CardHeader>
          <CardContent>
            {setupWork.pending ? (
              <Spinner label="Loading setup work" />
            ) : setupWork.error ? (
              <Stack gap="sm">
                <p role="alert">{setupWork.error.message}</p>
                <Button variant="secondary" onPress={() => setupWork.refresh()}>
                  Try again
                </Button>
              </Stack>
            ) : work.length === 0 ? (
              <p>No outstanding setup work.</p>
            ) : (
              <ol className="setup-work">
                {work.map((item) => (
                  <li>
                    <strong>{item.detail}</strong>
                    <span className="setup-work-source">
                      {' '}
                      ({item.sourceType.replaceAll('_', ' ')}
                      {item.sourceId ? ` ${item.sourceId.slice(0, 8)}` : ''})
                    </span>
                  </li>
                ))}
              </ol>
            )}
          </CardContent>
        </Card>
        <CriteriaCard program={current} onChanged={() => setVersion(version() + 1)} />
        <Card>
          <CardHeader>
            <CardTitle>Path to Type II</CardTitle>
          </CardHeader>
          <CardContent>
            <ol className="stage-plan">
              {current.stagePlan.map((stage) => (
                <li aria-current={stage.stage === current.stage ? 'step' : undefined}>
                  <strong>{stageLabel(stage.stage)}</strong>: {stage.advanceWhen}
                </li>
              ))}
            </ol>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Plan and targets</CardTitle>
            <CardDescription>
              Target dates are your planning goals. They are not dates confirmed by an audit firm.
              Last changed by {current.lastChangedBy} on {new Date(current.lastChangedAt).toLocaleDateString()}.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <PlanEditor program={current} onSaved={() => setVersion(version() + 1)} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Plan history</CardTitle>
          </CardHeader>
          <CardContent>
            {history.pending ? (
              <Spinner label="Loading history" />
            ) : history.error ? (
              <Stack gap="sm">
                <p role="alert">{history.error.message}</p>
                <Button variant="secondary" onPress={() => history.refresh()}>
                  Try again
                </Button>
              </Stack>
            ) : (
              <ol className="plain-list program-history">
                {(history.value ?? []).map((revision) => (
                  <li>
                    <strong>Revision {revision.revision}</strong> by {revision.actor} on{' '}
                    {new Date(revision.changedAt).toLocaleDateString()} ·{' '}
                    {planFields
                      .map((field) => `${field.label}: ${formatValue(revision.plan[field.key], field.type)}`)
                      .join('; ')}
                  </li>
                ))}
              </ol>
            )}
          </CardContent>
        </Card>
      </Stack>
    </Page>
  );
}
