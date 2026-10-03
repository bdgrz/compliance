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
import { createControlDraft, emptyContent, listControlDrafts, ProgramRequestError } from '../controls.js';
import { ControlForm } from './control-form.js';

export function ControlsPage({ programId }: { programId: string }) {
  const controls = resource(() => listControlDrafts(programId), [programId]);
  const programPath = organizationPath(`/programs/${programId}`);
  const back = <a href={programPath}>Back to program</a>;

  if (controls.pending && !controls.value) {
    return (
      <Page>
        <Spinner label="Loading controls" />
      </Page>
    );
  }

  if (controls.error) {
    const error = controls.error;
    const status = error instanceof ProgramRequestError ? error.status : null;
    return (
      <Page>
        <EmptyState
          title={status === 403 ? 'Controls are not available to you' : status === 404 ? 'Program not found' : 'Controls could not be loaded'}
          titleAs="h1"
          description={error.message}
          action={
            status === 403 || status === 404 ? (
              back
            ) : (
              <Button variant="primary" onPress={() => controls.refresh()}>
                Try again
              </Button>
            )
          }
        />
      </Page>
    );
  }

  const list = controls.value ?? [];

  async function create(identifier: string, content: typeof emptyContent) {
    const controlId = await createControlDraft(programId, identifier, content);
    window.location.assign(organizationPath(`/programs/${programId}/controls/${controlId}`));
    return null;
  }

  return (
    <Page>
      <PageHeader title="Controls" description="Draft controls and their implementation narratives before review." />
      <Stack gap="md">
        {back}
        <Card>
          <CardHeader>
            <CardTitle>Control inventory</CardTitle>
          </CardHeader>
          <CardContent>
            {list.length === 0 ? (
              <p>No controls yet. Draft the first control below.</p>
            ) : (
              <ul className="plain-list control-list">
                {list.map((control) => (
                  <li>
                    <a href={organizationPath(`/programs/${programId}/controls/${control.controlId}`)}>
                      <strong>{control.identifier}</strong> {control.content.title}
                    </a>
                    <span className="control-meta">
                      {' '}
                      {control.status.replaceAll('_', ' ')} · revision {control.revision} · owner{' '}
                      {control.ownerResolution.replaceAll('_', ' ')}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Draft a control</CardTitle>
            <CardDescription>Drafts can be revised freely; review and activation come later.</CardDescription>
          </CardHeader>
          <CardContent>
            <ControlForm initial={emptyContent} withIdentifier submitLabel="Create draft" onSubmit={create} />
          </CardContent>
        </Card>
      </Stack>
    </Page>
  );
}
