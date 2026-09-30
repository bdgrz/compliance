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
  getControlDraft,
  listControlDraftRevisions,
  ProgramRequestError,
  reviseControlDraft,
  type ControlContent,
} from '../controls.js';
import { ControlForm } from './control-form.js';

export function ControlDetailPage({ programId, controlId }: { programId: string; controlId: string }) {
  const [version, setVersion] = state(0);
  const [written, setWritten] = state<number | undefined>(undefined);
  const control = resource(() => getControlDraft(programId, controlId, written()), [programId, controlId, version()]);
  const history = resource(() => listControlDraftRevisions(programId, controlId), [programId, controlId, version()]);
  const back = <a href={organizationPath(`/programs/${programId}/controls`)}>Back to controls</a>;

  if (control.pending && !control.value) {
    return (
      <Page>
        <Spinner label="Loading control" />
      </Page>
    );
  }

  if (control.error) {
    const error = control.error;
    const status = error instanceof ProgramRequestError ? error.status : null;
    return (
      <Page>
        <EmptyState
          title={status === 403 ? 'This control is not available to you' : status === 404 ? 'Control not found' : 'Control could not be loaded'}
          titleAs="h1"
          description={error.message}
          action={
            status === 403 || status === 404 ? (
              back
            ) : (
              <Button variant="primary" onPress={() => control.refresh()}>
                Try again
              </Button>
            )
          }
        />
      </Page>
    );
  }

  const current = control.value!;

  async function save(_identifier: string, content: ControlContent) {
    await reviseControlDraft(programId, controlId, current.revision, content);
    setWritten(current.revision + 1);
    setVersion(version() + 1);
    return 'Control draft saved.';
  }

  return (
    <Page>
      <PageHeader
        title={`${current.identifier} ${current.content.title}`}
        description={`Status: ${current.status.replaceAll('_', ' ')} · revision ${current.revision} · last changed by ${current.lastChangedBy} on ${new Date(current.lastChangedAt).toLocaleDateString()}`}
      />
      <Stack gap="md">
        {back}
        <Card>
          <CardHeader>
            <CardTitle>Edit draft</CardTitle>
            <CardDescription>
              Saving checks that nobody else changed revision {current.revision} since you opened it.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <ControlForm initial={current.content} withIdentifier={false} submitLabel="Save draft" onSubmit={save} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Applicability</CardTitle>
            <CardDescription>
              Owner {current.ownerResolution.replaceAll('_', ' ')} · applicability{' '}
              {current.applicabilityResolution.replaceAll('_', ' ')}
            </CardDescription>
          </CardHeader>
          <CardContent>
            {current.content.applicability.length === 0 ? (
              <p>No applicability references yet.</p>
            ) : (
              <ul className="plain-list control-applicability">
                {current.content.applicability.map((reference) => (
                  <li>
                    <strong>{reference.subject}</strong> ({reference.subject_type.replaceAll('_', ' ')}
                    {reference.unresolved ? ', not yet linked to a governed record' : ''}): {reference.rationale}
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Draft history</CardTitle>
          </CardHeader>
          <CardContent>
            {history.pending && !history.value ? (
              <Spinner label="Loading history" />
            ) : history.error ? (
              <Stack gap="sm">
                <p role="alert">{history.error.message}</p>
                <Button variant="secondary" onPress={() => history.refresh()}>
                  Try again
                </Button>
              </Stack>
            ) : (
              <ol className="plain-list control-history">
                {(history.value ?? []).map((revision) => (
                  <li>
                    <strong>Revision {revision.revision}</strong> by {revision.actor} on{' '}
                    {new Date(revision.changedAt).toLocaleDateString()} · {revision.content.title} ·{' '}
                    {revision.content.expectedEvidence.length} expected evidence
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
