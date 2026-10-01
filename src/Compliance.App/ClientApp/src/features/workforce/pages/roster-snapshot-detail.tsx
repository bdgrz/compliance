import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
  Button,
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
  amendRosterSnapshot,
  formatDate,
  getRosterSnapshot,
  lifecycleStatuses,
  optionLabel,
  workerTypes,
  type RosterSnapshot,
} from '../workforce.js';
import { inputValue, RecordFailure } from '../workforce-shared.js';
import { shortHash, snapshotPath } from './roster-snapshots.js';

function AmendForm({ snapshot }: { snapshot: RosterSnapshot }) {
  const [reason, setReason] = state('');
  const [pending, setPending] = state(false);
  const [error, setError] = state<string | null>(null);
  const [amendedId, setAmendedId] = state<string | null>(null);

  async function amend(event: Event) {
    event.preventDefault();
    setError(null);
    if (reason().trim().length === 0) {
      setError('Give a reason for the amendment.');
      return;
    }
    setPending(true);
    try {
      setAmendedId(await amendRosterSnapshot(snapshot.snapshotId, reason()));
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Unable to amend this snapshot.');
    } finally {
      setPending(false);
    }
  }

  return (
    <form onSubmit={(event: Event) => void amend(event)} aria-label="Amend snapshot">
      <Stack gap="sm">
        <label className="registration-field">
          <span>Reason for the amendment</span>
          <textarea value={reason()} maxLength={1000} onInput={(event: Event) => setReason(inputValue(event))} required />
        </label>
        {error() ? <p role="alert">{error()}</p> : null}
        {amendedId() ? (
          <p role="status">
            Amendment recorded. <a href={snapshotPath(amendedId()!)}>Open the amendment</a>.
          </p>
        ) : null}
        <Button variant="secondary" type="submit" disabled={pending() || amendedId() !== null}>
          {pending() ? 'Amending…' : 'Freeze an amendment'}
        </Button>
      </Stack>
    </form>
  );
}

export function RosterSnapshotDetailPage({ snapshotId }: { snapshotId: string }) {
  const snapshot = resource(() => getRosterSnapshot(snapshotId), [snapshotId]);

  if (snapshot.pending && !snapshot.value) {
    return (
      <Page>
        <Spinner label="Loading workforce snapshot" />
      </Page>
    );
  }

  if (snapshot.error) {
    return (
      <RecordFailure
        error={snapshot.error}
        noun="workforce snapshot"
        backPath="/workforce/snapshots"
        backLabel="Back to workforce snapshots"
        onRetry={() => snapshot.refresh()}
      />
    );
  }

  const current = snapshot.value!;
  const names = new Map(current.people.map((person) => [person.personId, person.displayName]));
  return (
    <Page>
      <PageHeader
        title={`Workforce snapshot ${shortHash(current.contentSha256)}`}
        description={`Frozen ${new Date(current.frozenAt).toLocaleString()} by ${current.frozenBy} · ${current.rowCount} rows`}
      />
      <Stack gap="md">
        <a href={organizationPath('/workforce/snapshots')}>Back to workforce snapshots</a>
        <Card>
          <CardHeader>
            <CardTitle>Identity and lineage</CardTitle>
            <CardDescription>The content hash proves the frozen rows have not changed since.</CardDescription>
          </CardHeader>
          <CardContent>
            <dl className="workforce-facts">
              <dt>Content SHA-256</dt>
              <dd>
                <code className="workforce-hash">{current.contentSha256}</code>
              </dd>
              <dt>Kind</dt>
              <dd>{current.amendsSnapshotId ? 'Amendment' : 'Original snapshot'}</dd>
              {current.amendsSnapshotId ? (
                <>
                  <dt>Amends</dt>
                  <dd>
                    <a href={snapshotPath(current.amendsSnapshotId)}>The snapshot it corrects</a>
                  </dd>
                  <dt>Reason</dt>
                  <dd>{current.amendmentReason}</dd>
                </>
              ) : null}
              {current.rootSnapshotId !== current.snapshotId ? (
                <>
                  <dt>Original</dt>
                  <dd>
                    <a href={snapshotPath(current.rootSnapshotId)}>The first snapshot in this lineage</a>
                  </dd>
                </>
              ) : null}
            </dl>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>People</CardTitle>
            <CardDescription>As-of source facts: each row records the revision that was frozen.</CardDescription>
          </CardHeader>
          <CardContent>
            {current.people.length === 0 ? (
              <p>No people were on the roster.</p>
            ) : (
              <table className="inventory-table workforce-table">
                <caption className="visually-hidden">People in this snapshot</caption>
                <thead>
                  <tr>
                    <th scope="col">Name</th>
                    <th scope="col">Work email</th>
                    <th scope="col">Revision</th>
                  </tr>
                </thead>
                <tbody>
                  {current.people.map((person) => (
                    <tr>
                      <td>{person.displayName}</td>
                      <td>{person.workEmail ?? '—'}</td>
                      <td>{person.revision}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Work relationships</CardTitle>
            <CardDescription>
              {current.restrictedFieldsRedacted
                ? 'Managers and sponsors are restricted fields and are withheld from you.'
                : 'Includes managers and sponsors, which are restricted fields.'}
            </CardDescription>
          </CardHeader>
          <CardContent>
            {current.relationships.length === 0 ? (
              <p>No work relationships were on the roster.</p>
            ) : (
              <table className="inventory-table workforce-table">
                <caption className="visually-hidden">Work relationships in this snapshot</caption>
                <thead>
                  <tr>
                    <th scope="col">Person</th>
                    <th scope="col">Worker ID</th>
                    <th scope="col">Type</th>
                    <th scope="col">Status</th>
                    <th scope="col">Dates</th>
                    <th scope="col">Department</th>
                    {current.restrictedFieldsRedacted ? null : <th scope="col">Manager</th>}
                    <th scope="col">Revision</th>
                  </tr>
                </thead>
                <tbody>
                  {current.relationships.map((job) => (
                    <tr>
                      <td>{names.get(job.personId) ?? 'Unknown person'}</td>
                      <td>{job.sourceWorkerId}</td>
                      <td>{optionLabel(workerTypes, job.workerType)}</td>
                      <td>{optionLabel(lifecycleStatuses, job.lifecycleStatus)}</td>
                      <td>
                        {formatDate(job.startDate)}
                        {job.endDate ? ` – ${formatDate(job.endDate)}` : ''}
                      </td>
                      <td>{job.department ?? '—'}</td>
                      {current.restrictedFieldsRedacted ? null : (
                        <td>{job.managerPersonId ? (names.get(job.managerPersonId) ?? 'Unknown person') : '—'}</td>
                      )}
                      <td>{job.revision}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Amend</CardTitle>
            <CardDescription>
              Freezes the current roster as a correction of this snapshot. This snapshot stays unchanged.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <AmendForm snapshot={current} />
          </CardContent>
        </Card>
      </Stack>
    </Page>
  );
}
