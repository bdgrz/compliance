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
  freezeRosterSnapshot,
  getRosterSnapshotAsOf,
  listRosterSnapshots,
  WorkforceRequestError,
  type RosterSnapshot,
} from '../workforce.js';
import { inputValue, LoadFailure, WorkforceForbidden, WorkforceSections } from '../workforce-shared.js';

export function snapshotPath(snapshotId: string): string {
  return organizationPath(`/workforce/snapshots/${snapshotId}`);
}

export function shortHash(hash: string): string {
  return hash.slice(0, 12);
}

// Finds the snapshot that was in force at a past moment: evidence for "who was on the roster then".
function AsOfLookup() {
  const [when, setWhen] = state('');
  const [pending, setPending] = state(false);
  const [error, setError] = state<string | null>(null);
  const [found, setFound] = state<RosterSnapshot | null>(null);

  async function lookUp(event: Event) {
    event.preventDefault();
    setError(null);
    setFound(null);
    if (!when()) return;
    setPending(true);
    try {
      setFound(await getRosterSnapshotAsOf(new Date(when()).toISOString()));
    } catch (failure) {
      setError(
        failure instanceof WorkforceRequestError && failure.status === 404
          ? 'No roster snapshot was frozen at or before that time.'
          : failure instanceof Error
            ? failure.message
            : 'Unable to look up the roster.'
      );
    } finally {
      setPending(false);
    }
  }

  const snapshot = found();
  return (
    <form onSubmit={(event: Event) => void lookUp(event)} aria-label="Find the roster as of a time">
      <Stack gap="sm">
        <label className="registration-field">
          <span>Roster as of</span>
          <input type="datetime-local" value={when()} onInput={(event: Event) => setWhen(inputValue(event))} required />
        </label>
        <Button variant="secondary" type="submit" disabled={pending()}>
          {pending() ? 'Looking up…' : 'Find snapshot'}
        </Button>
        {error() ? <p role="alert">{error()}</p> : null}
        {snapshot ? (
          <p role="status">
            In force then: <a href={snapshotPath(snapshot.snapshotId)}>snapshot {shortHash(snapshot.contentSha256)}</a>,
            frozen {new Date(snapshot.frozenAt).toLocaleString()} by {snapshot.frozenBy} with {snapshot.people.length}{' '}
            people and {snapshot.relationships.length} work relationships.
          </p>
        ) : null}
      </Stack>
    </form>
  );
}

export function RosterSnapshotsPage() {
  const [version, setVersion] = state(0);
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<string | null>(null);
  const [frozenId, setFrozenId] = state<string | null>(null);
  const snapshots = resource(() => listRosterSnapshots(), [version()]);

  if (snapshots.error instanceof WorkforceRequestError && snapshots.error.status === 403) {
    return <WorkforceForbidden title="Workforce snapshots are not available to you" />;
  }

  async function freeze() {
    setActionError(null);
    setFrozenId(null);
    setPending(true);
    try {
      setFrozenId(await freezeRosterSnapshot());
      setVersion(version() + 1);
    } catch (failure) {
      setActionError(failure instanceof Error ? failure.message : 'Unable to freeze the roster.');
    } finally {
      setPending(false);
    }
  }

  const items = snapshots.value ?? [];
  return (
    <Page>
      <PageHeader
        title="Workforce snapshots"
        description="Frozen, hash-verified copies of the roster used as audit evidence."
      />
      <Stack gap="md">
        <WorkforceSections current="/workforce/snapshots" />
        <Card>
          <CardHeader>
            <CardTitle>Freeze the roster</CardTitle>
            <CardDescription>
              A snapshot records every person and work relationship as they stand now. It never changes; corrections
              are recorded as amendments that keep the original.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Stack gap="sm">
              {actionError() ? <p role="alert">{actionError()}</p> : null}
              {frozenId() ? (
                <p role="status">
                  Roster frozen. <a href={snapshotPath(frozenId()!)}>Open the new snapshot</a>.
                </p>
              ) : null}
              <Button variant="primary" onPress={() => void freeze()} disabled={pending()}>
                {pending() ? 'Freezing…' : 'Freeze roster now'}
              </Button>
            </Stack>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Find the roster at a past time</CardTitle>
            <CardDescription>Returns the latest snapshot or amendment frozen at or before that moment.</CardDescription>
          </CardHeader>
          <CardContent>
            <AsOfLookup />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Snapshots</CardTitle>
            <CardDescription>Newest first. Amendments link to the snapshot they correct.</CardDescription>
          </CardHeader>
          <CardContent>
            {snapshots.pending && !snapshots.value ? (
              <Spinner label="Loading workforce snapshots" />
            ) : snapshots.error ? (
              <LoadFailure error={snapshots.error} onRetry={() => snapshots.refresh()} />
            ) : items.length === 0 ? (
              <p>No workforce snapshots frozen yet.</p>
            ) : (
              <table className="inventory-table workforce-table">
                <caption className="visually-hidden">Workforce snapshots</caption>
                <thead>
                  <tr>
                    <th scope="col">Snapshot</th>
                    <th scope="col">Kind</th>
                    <th scope="col">Rows</th>
                    <th scope="col">Frozen</th>
                    <th scope="col">Amendment reason</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <tr>
                      <td>
                        <a href={snapshotPath(item.snapshotId)}>
                          <code>{shortHash(item.contentSha256)}</code>
                        </a>
                      </td>
                      <td>{item.amendsSnapshotId ? 'Amendment' : 'Original'}</td>
                      <td>{item.rowCount}</td>
                      <td>
                        {new Date(item.frozenAt).toLocaleString()} by {item.frozenBy}
                      </td>
                      <td>{item.amendmentReason ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </CardContent>
        </Card>
      </Stack>
    </Page>
  );
}
