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
import { emptyTerms, RelationshipFields } from '../relationship-fields.js';
import {
  formatDate,
  lifecycleStatuses,
  listPeople,
  listWorkRelationships,
  optionLabel,
  recordPerson,
  recordWorkRelationship,
  sourceKindLabel,
  workerTypes,
  WorkforceRequestError,
  type Person,
  type WorkRelationshipTerms,
} from '../workforce.js';
import { ActionError, inputValue, LoadFailure, WorkforceForbidden, WorkforceSections } from '../workforce-shared.js';

function RecordPersonForm({ onRecorded }: { onRecorded: () => void }) {
  const [name, setName] = state('');
  const [email, setEmail] = state('');
  const [pending, setPending] = state(false);
  const [error, setError] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  async function submit(event: Event) {
    event.preventDefault();
    setError(null);
    setNotice(null);
    setPending(true);
    try {
      await recordPerson(name(), email() || null);
      setNotice(`${name().trim()} was added to the roster.`);
      setName('');
      setEmail('');
      onRecorded();
    } catch (failure) {
      setError(failure instanceof Error ? failure : new Error('Unable to record the person.'));
    } finally {
      setPending(false);
    }
  }

  return (
    <form onSubmit={(event: Event) => void submit(event)}>
      <Stack gap="sm">
        <label className="registration-field">
          <span>Display name</span>
          <input type="text" value={name()} maxLength={200} onInput={(event: Event) => setName(inputValue(event))} required />
        </label>
        <label className="registration-field">
          <span>Work email (optional, never used as an identifier)</span>
          <input type="email" value={email()} onInput={(event: Event) => setEmail(inputValue(event))} />
        </label>
        <ActionError error={error()} noun="person" />
        {notice() ? <p role="status">{notice()}</p> : null}
        <Button variant="primary" type="submit" disabled={pending()}>
          {pending() ? 'Recording…' : 'Record person'}
        </Button>
      </Stack>
    </form>
  );
}

function RecordRelationshipForm({ people, onRecorded }: { people: Person[]; onRecorded: () => void }) {
  const [personId, setPersonId] = state('');
  const [workerId, setWorkerId] = state('');
  const [terms, setTerms] = state<WorkRelationshipTerms>({ ...emptyTerms });
  const [pending, setPending] = state(false);
  const [error, setError] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  async function submit(event: Event) {
    event.preventDefault();
    setError(null);
    setNotice(null);
    setPending(true);
    try {
      await recordWorkRelationship(personId(), workerId(), terms());
      setNotice(`Work relationship ${workerId().trim()} was recorded.`);
      setPersonId('');
      setWorkerId('');
      setTerms({ ...emptyTerms });
      onRecorded();
    } catch (failure) {
      setError(failure instanceof Error ? failure : new Error('Unable to record the work relationship.'));
    } finally {
      setPending(false);
    }
  }

  if (people.length === 0) {
    return <p>Record a person first; every work relationship belongs to a person on the roster.</p>;
  }

  return (
    <form onSubmit={(event: Event) => void submit(event)}>
      <Stack gap="sm">
        <label className="registration-field">
          <span>Person</span>
          <select value={personId()} onChange={(event: Event) => setPersonId(inputValue(event))} required>
            <option value="">Choose a person</option>
            {people.map((person) => (
              <option value={person.personId}>{person.displayName}</option>
            ))}
          </select>
        </label>
        <label className="registration-field">
          <span>Source worker ID</span>
          <input type="text" value={workerId()} onInput={(event: Event) => setWorkerId(inputValue(event))} required />
        </label>
        <RelationshipFields terms={terms()} people={people} onChange={setTerms} showManager />
        <ActionError error={error()} noun="work relationship" />
        {notice() ? <p role="status">{notice()}</p> : null}
        <Button variant="primary" type="submit" disabled={pending()}>
          {pending() ? 'Recording…' : 'Record work relationship'}
        </Button>
      </Stack>
    </form>
  );
}

export function WorkforceRosterPage() {
  const [version, setVersion] = state(0);
  const people = resource(() => listPeople(), [version()]);
  const relationships = resource(() => listWorkRelationships(), [version()]);
  const reload = () => setVersion(version() + 1);

  if (people.error instanceof WorkforceRequestError && people.error.status === 403) {
    return <WorkforceForbidden title="The workforce roster is not available to you" />;
  }

  const roster = people.value ?? [];
  const names = new Map(roster.map((person) => [person.personId, person.displayName]));

  return (
    <Page>
      <PageHeader
        title="Workforce"
        description="The manually maintained roster is the authoritative workforce source until an HRIS import exists."
      />
      <Stack gap="md">
        <WorkforceSections current="/workforce" />
        <Card>
          <CardHeader>
            <CardTitle>People</CardTitle>
            <CardDescription>
              Source precedence: manual roster entries are authoritative. A person need not be a platform member.
            </CardDescription>
          </CardHeader>
          <CardContent>
            {people.pending && !people.value ? (
              <Spinner label="Loading people" />
            ) : people.error ? (
              <LoadFailure error={people.error} onRetry={() => people.refresh()} />
            ) : roster.length === 0 ? (
              <p>No people on the roster yet. Record the first person below.</p>
            ) : (
              <ul className="plain-list workforce-list">
                {roster.map((person) => (
                  <li>
                    <a href={organizationPath(`/workforce/people/${person.personId}`)}>{person.displayName}</a>
                    {person.workEmail ? ` · ${person.workEmail}` : ''} · {sourceKindLabel(person.sourceKind)}
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Record a person</CardTitle>
          </CardHeader>
          <CardContent>
            <RecordPersonForm onRecorded={reload} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Work relationships</CardTitle>
            <CardDescription>
              Managers are restricted and appear only when you open a single relationship.
            </CardDescription>
          </CardHeader>
          <CardContent>
            {relationships.pending && !relationships.value ? (
              <Spinner label="Loading work relationships" />
            ) : relationships.error ? (
              <LoadFailure error={relationships.error} onRetry={() => relationships.refresh()} />
            ) : (relationships.value ?? []).length === 0 ? (
              <p>No work relationships yet.</p>
            ) : (
              <table className="inventory-table workforce-table">
                <caption className="visually-hidden">Work relationships</caption>
                <thead>
                  <tr>
                    <th scope="col">Worker ID</th>
                    <th scope="col">Person</th>
                    <th scope="col">Type</th>
                    <th scope="col">Status</th>
                    <th scope="col">Dates</th>
                    <th scope="col">Department</th>
                  </tr>
                </thead>
                <tbody>
                  {(relationships.value ?? []).map((relationship) => (
                    <tr>
                      <td>
                        <a href={organizationPath(`/workforce/relationships/${relationship.relationshipId}`)}>
                          {relationship.sourceWorkerId}
                        </a>
                      </td>
                      <td>{names.get(relationship.personId) ?? 'Unknown person'}</td>
                      <td>{optionLabel(workerTypes, relationship.workerType)}</td>
                      <td>{optionLabel(lifecycleStatuses, relationship.lifecycleStatus)}</td>
                      <td>
                        {formatDate(relationship.startDate)}
                        {relationship.endDate ? ` – ${formatDate(relationship.endDate)}` : ''}
                      </td>
                      <td>{relationship.department ?? 'Not set'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Record a work relationship</CardTitle>
            <CardDescription>A source worker ID identifies exactly one relationship.</CardDescription>
          </CardHeader>
          <CardContent>
            {people.value ? <RecordRelationshipForm people={roster} onRecorded={reload} /> : null}
          </CardContent>
        </Card>
      </Stack>
    </Page>
  );
}
