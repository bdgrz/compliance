import { resource } from '@askrjs/askr/resources';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  Page,
  PageHeader,
  Spinner,
} from '@askrjs/themes/components';

import { organizationPath } from '../../tenants/tenants.js';
import {
  listSourceObservations,
  sourceLabel,
  sourceTargetPath,
} from '../source-observations.js';
import {
  LoadFailure,
  WorkforceForbidden,
  WorkforceSections,
} from '../workforce-shared.js';
import { WorkforceRequestError } from '../workforce.js';

export function WorkforceSourcesPage() {
  const observations = resource(() => listSourceObservations(), []);
  if (
    observations.error instanceof WorkforceRequestError &&
    observations.error.status === 403
  ) {
    return (
      <WorkforceForbidden title="Source observations are not available to you" />
    );
  }
  return (
    <Page>
      <PageHeader
        title="Workforce sources"
        description="Manually entered evidence from HRIS, identity providers, and providers."
      />
      <WorkforceSections current="/workforce/sources" />
      <Card>
        <CardHeader>
          <CardTitle>Source observations</CardTitle>
        </CardHeader>
        <CardContent>
          <p>
            HRIS is authoritative for workforce facts. Identity providers and
            providers corroborate facts. Recording or accepting evidence never
            changes canonical records or access automatically.
          </p>
          {observations.pending ? (
            <Spinner label="Loading source observations" />
          ) : observations.error ? (
            <LoadFailure
              error={observations.error}
              onRetry={() => observations.refresh()}
            />
          ) : observations.value?.length ? (
            <table className="inventory-table workforce-table">
              <caption className="visually-hidden">
                Workforce source observations
              </caption>
              <thead>
                <tr>
                  <th scope="col">Source</th>
                  <th scope="col">Source record</th>
                  <th scope="col">Canonical record</th>
                  <th scope="col">Decision</th>
                </tr>
              </thead>
              <tbody>
                {observations.value.map((item) => (
                  <tr>
                    <td>
                      {sourceLabel(item.source.source_kind)} ·{' '}
                      {item.source.source_system}
                    </td>
                    <td>
                      <a
                        href={organizationPath(
                          `/workforce/sources/${item.observation_id}`
                        )}
                      >
                        {item.source.source_record_id} ·{' '}
                        {item.source.source_revision}
                      </a>
                    </td>
                    <td>
                      <a
                        href={organizationPath(
                          sourceTargetPath(item.target_kind, item.target_id)
                        )}
                      >
                        {item.target_kind.replaceAll('_', ' ')}
                      </a>
                    </td>
                    <td>
                      {item.decision?.outcome === 'accepted'
                        ? `Accepted for revision ${item.decision.target_revision}`
                        : item.decision?.outcome === 'dismissed'
                          ? 'Dismissed'
                          : 'Needs review'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <p>
              No source observations yet. Open a canonical workforce record to
              record source evidence.
            </p>
          )}
        </CardContent>
      </Card>
    </Page>
  );
}
