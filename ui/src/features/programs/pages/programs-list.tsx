import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
  Button,
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  EmptyState,
  Page,
  PageHeader,
  Spinner,
  Stack,
} from '@askrjs/themes/components';

import { organizationPath } from '../../tenants/tenants.js';
import { createProgram, emptyPlan, listPrograms, ProgramRequestError, stageLabel } from '../programs.js';

export function ProgramsPage() {
  const [name, setName] = state('');
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<string | null>(null);
  const programs = resource(() => listPrograms(), []);

  async function create(event: Event) {
    event.preventDefault();
    setActionError(null);
    setPending(true);
    try {
      const programId = await createProgram(name().trim(), emptyPlan);
      window.location.assign(organizationPath(`/programs/${programId}`));
    } catch (failure) {
      setActionError(failure instanceof Error ? failure.message : 'Unable to create the program.');
      setPending(false);
    }
  }

  if (programs.error instanceof ProgramRequestError && programs.error.status === 403) {
    return (
      <Page>
        <EmptyState
          title="Programs are not available to you"
          titleAs="h1"
          description="Ask a Compliance Lead or Org Admin for access to this organization's programs."
        />
      </Page>
    );
  }

  return (
    <Page>
      <PageHeader title="Programs" description="Each program carries one SOC 2 journey from readiness to Type II." />
      <Card>
        <CardHeader>
          <CardTitle>Start a program</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={(event: Event) => void create(event)}>
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
              {actionError() ? <p role="alert">{actionError()}</p> : null}
              <Button variant="primary" type="submit" disabled={pending()}>
                {pending() ? 'Starting…' : 'Start program'}
              </Button>
            </Stack>
          </form>
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>Your programs</CardTitle>
        </CardHeader>
        <CardContent>
          {programs.pending ? (
            <Spinner label="Loading programs" />
          ) : programs.error ? (
            <Stack gap="sm">
              <p role="alert">{programs.error.message}</p>
              <Button variant="secondary" onPress={() => programs.refresh()}>
                Try again
              </Button>
            </Stack>
          ) : (programs.value ?? []).length === 0 ? (
            <p>No programs yet. Start one to plan your path to SOC 2.</p>
          ) : (
            <ul className="plain-list">
              {(programs.value ?? []).map((program) => (
                <li>
                  <a href={organizationPath(`/programs/${program.programId}`)}>{program.name}</a> ·{' '}
                  {stageLabel(program.stage)}
                  {program.nextStage ? ` → ${stageLabel(program.nextStage)}` : ''}
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </Page>
  );
}
