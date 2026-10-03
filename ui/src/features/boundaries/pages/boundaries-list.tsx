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
  createBoundary,
  emptyBoundaryContent,
  label,
  listBoundaries,
  ProgramRequestError,
  type BoundaryContent,
} from '../boundaries.js';
import { BoundaryForm } from '../components/boundary-form.js';

export function BoundariesPage({ programId }: { programId: string }) {
  const boundaries = resource(() => listBoundaries(programId), [programId]);
  const back = <a href={organizationPath(`/programs/${programId}`)}>Back to program</a>;

  if (boundaries.pending && !boundaries.value) {
    return (
      <Page>
        <Spinner label="Loading boundaries" />
      </Page>
    );
  }

  if (boundaries.error) {
    const error = boundaries.error;
    const status = error instanceof ProgramRequestError ? error.status : null;
    return (
      <Page>
        <EmptyState
          title={
            status === 403
              ? 'Boundaries are not available to you'
              : status === 404
                ? 'Program not found'
                : 'Boundaries could not be loaded'
          }
          titleAs="h1"
          description={error.message}
          action={
            status === 403 || status === 404 ? (
              back
            ) : (
              <Button variant="primary" onPress={() => boundaries.refresh()}>
                Try again
              </Button>
            )
          }
        />
      </Page>
    );
  }

  const list = boundaries.value ?? [];

  async function create(content: BoundaryContent) {
    const boundaryId = await createBoundary(programId, content);
    window.location.assign(organizationPath(`/programs/${programId}/boundaries/${boundaryId}`));
    return null;
  }

  return (
    <Page>
      <PageHeader
        title="System boundaries"
        description="Author the in-scope services, systems, people, and assumptions, then review and approve each version."
      />
      <Stack gap="md">
        {back}
        <Card>
          <CardHeader>
            <CardTitle>Boundaries in this program</CardTitle>
          </CardHeader>
          <CardContent>
            {list.length === 0 ? (
              <p>No boundaries yet. Draft the first boundary below.</p>
            ) : (
              <ul className="plain-list boundary-list">
                {list.map((boundary) => {
                  const shown = boundary.draft ?? boundary.approved;
                  return (
                    <li>
                      <a href={organizationPath(`/programs/${programId}/boundaries/${boundary.boundaryId}`)}>
                        {shown?.content.statement.slice(0, 120) || 'Untitled boundary'}
                      </a>
                      <span className="boundary-meta">
                        {' '}
                        {boundary.draft ? `draft revision ${boundary.draft.revision}` : 'no open draft'}
                        {boundary.approved ? ` · approved revision ${boundary.approved.revision}` : ' · never approved'}
                        {boundary.latestDecision ? ` · last decision: ${label(boundary.latestDecision.outcome)}` : ''}
                      </span>
                    </li>
                  );
                })}
              </ul>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Draft a boundary</CardTitle>
            <CardDescription>Security is always in scope; add optional categories as the engagement requires.</CardDescription>
          </CardHeader>
          <CardContent>
            <BoundaryForm initial={emptyBoundaryContent} submitLabel="Create draft" onSubmit={create} />
          </CardContent>
        </Card>
      </Stack>
    </Page>
  );
}
