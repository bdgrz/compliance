import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  Page,
  PageHeader,
  Spinner,
  Stack,
} from '@askrjs/themes/components';

import { organizationPath } from '../../tenants/tenants.js';
import {
  formatDate,
  listPeople,
  listWorkforceObservations,
  observationKinds,
  optionLabel,
  WorkforceRequestError,
} from '../workforce.js';
import { ResolutionSummary, ResolveObservationForm } from '../observation-resolution.js';
import { inputValue, LoadFailure, WorkforceForbidden, WorkforceSections } from '../workforce-shared.js';

export function WorkforceObservationsPage() {
  const [kind, setKind] = state('');
  const [version, setVersion] = state(0);
  const [notice, setNotice] = state<string | null>(null);
  const observations = resource(() => listWorkforceObservations(kind() || null), [kind(), version()]);
  const people = resource(() => listPeople().catch(() => []), []);

  if (observations.error instanceof WorkforceRequestError && observations.error.status === 403) {
    return <WorkforceForbidden title="Workforce changes are not available to you" />;
  }

  const names = new Map((people.value ?? []).map((person) => [person.personId, person.displayName]));
  const items = observations.value ?? [];

  return (
    <Page>
      <PageHeader
        title="Joiners, movers, and leavers"
        description="Changes observed in the workforce roster, opened as compliance work."
      />
      <Stack gap="md">
        <WorkforceSections current="/workforce/observations" />
        <p className="workforce-callout" role="note">
          These observations are compliance work only. They never grant, change, or revoke anyone's access.
        </p>
        <Card>
          <CardHeader>
            <CardTitle>Observed changes</CardTitle>
            <CardDescription>
              Observed from each accepted revision of a work relationship in the manual roster. Manager-only changes are
              restricted and produce no observation.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Stack gap="sm">
              {notice() ? <p role="status">{notice()}</p> : null}
              <label className="registration-field">
                <span>Show</span>
                <select value={kind()} onChange={(event: Event) => setKind(inputValue(event))}>
                  <option value="">All changes</option>
                  {observationKinds.map((option) => (
                    <option value={option.value}>{option.label}s</option>
                  ))}
                </select>
              </label>
              {observations.pending && !observations.value ? (
                <Spinner label="Loading workforce changes" />
              ) : observations.error ? (
                <LoadFailure error={observations.error} onRetry={() => observations.refresh()} />
              ) : items.length === 0 ? (
                <p>
                  {kind()
                    ? `No ${optionLabel(observationKinds, kind()).toLowerCase()} observations.`
                    : 'No workforce changes observed yet.'}
                </p>
              ) : (
                <table className="inventory-table workforce-table">
                  <caption className="visually-hidden">Observed workforce changes</caption>
                  <thead>
                    <tr>
                      <th scope="col">Change</th>
                      <th scope="col">Person</th>
                      <th scope="col">Worker ID</th>
                      <th scope="col">Effective</th>
                      <th scope="col">Changed fields</th>
                      <th scope="col">Status</th>
                      <th scope="col">Observed</th>
                    </tr>
                  </thead>
                  <tbody>
                    {items.map((item) => (
                      <tr>
                        <td>
                          <span className={`workforce-kind workforce-kind-${item.kind}`}>
                            {optionLabel(observationKinds, item.kind)}
                          </span>
                        </td>
                        <td>{names.get(item.personId) ?? 'Unknown person'}</td>
                        <td>
                          <a href={organizationPath(`/workforce/relationships/${item.relationshipId}`)}>
                            {item.sourceWorkerId}
                          </a>
                        </td>
                        <td>{formatDate(item.effectiveDate)}</td>
                        <td>
                          {item.changedFields.length > 0
                            ? item.changedFields.map((field) => field.replaceAll('_', ' ')).join(', ')
                            : '—'}
                        </td>
                        <td>
                          {item.resolution ? (
                            <ResolutionSummary resolution={item.resolution} />
                          ) : item.status === 'open' ? (
                            <details className="workforce-close">
                              <summary>Open · close…</summary>
                              <ResolveObservationForm
                                observationId={item.observationId}
                                label={`${optionLabel(observationKinds, item.kind).toLowerCase()} ${item.sourceWorkerId}`}
                                onClosed={(message) => {
                                  setNotice(message);
                                  setVersion(version() + 1);
                                }}
                              />
                            </details>
                          ) : (
                            item.status.replaceAll('_', ' ')
                          )}
                        </td>
                        <td>
                          {new Date(item.observedAt).toLocaleString()} from {item.observedFrom}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </Stack>
          </CardContent>
        </Card>
      </Stack>
    </Page>
  );
}
