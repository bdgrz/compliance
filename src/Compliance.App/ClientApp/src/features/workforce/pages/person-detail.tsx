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

import { listMembers, memberLabel } from '../../members/members.js';
import { organizationPath } from '../../tenants/tenants.js';
import { correlatePersonMembership, getPerson, revisePerson, sourceKindLabel, type Person } from '../workforce.js';
import { ActionError, inputValue, LoadFailure, RecordFailure } from '../workforce-shared.js';
import { SourceObservationsPanel } from '../source-record.js';

function PersonEditor({ person, onSaving, onSaved }: { key?: string; person: Person; onSaving: () => void; onSaved: () => void }) {
  const [name, setName] = state(person.displayName);
  const [email, setEmail] = state(person.workEmail ?? '');
  const [pending, setPending] = state(false);
  const [error, setError] = state<Error | null>(null);

  async function save(event: Event) {
    event.preventDefault();
    setError(null);
    onSaving();
    setPending(true);
    try {
      await revisePerson(person.personId, person.revision, name(), email() || null);
      onSaved();
    } catch (failure) {
      setError(failure instanceof Error ? failure : new Error('Unable to save this person.'));
    } finally {
      setPending(false);
    }
  }

  return (
    <form onSubmit={(event: Event) => void save(event)}>
      <Stack gap="sm">
        <label className="registration-field">
          <span>Display name</span>
          <input type="text" value={name()} maxLength={200} onInput={(event: Event) => setName(inputValue(event))} required />
        </label>
        <label className="registration-field">
          <span>Work email (optional)</span>
          <input type="email" value={email()} onInput={(event: Event) => setEmail(inputValue(event))} />
        </label>
        <ActionError error={error()} noun="person" />
        <Button variant="primary" type="submit" disabled={pending()}>
          {pending() ? 'Saving…' : 'Save person'}
        </Button>
      </Stack>
    </form>
  );
}

// Links this roster person to the organization member who is the same human, so reconciliation can
// compare the roster with access. Firm staff are not tenant workforce and are never offered.
function MembershipCorrelation({ person, onSaved }: { person: Person; onSaved: () => void }) {
  const members = resource(() => listMembers(), []);
  const [userId, setUserId] = state(person.correlatedUserId ?? '');
  const [pending, setPending] = state(false);
  const [error, setError] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  async function save(event: Event) {
    event.preventDefault();
    setError(null);
    setNotice(null);
    setPending(true);
    try {
      await correlatePersonMembership(person.personId, person.revision, userId() || null);
      setNotice(userId() ? 'Member correlation saved.' : 'Member correlation cleared.');
      onSaved();
    } catch (failure) {
      setError(failure instanceof Error ? failure : new Error('Unable to save the correlation.'));
    } finally {
      setPending(false);
    }
  }

  if (members.pending && !members.value) return <Spinner label="Loading members" />;
  if (members.error) return <LoadFailure error={members.error} onRetry={() => members.refresh()} />;
  const workforce = (members.value ?? []).filter((member) => member.affiliation !== 'firm_staff');
  const current = (members.value ?? []).find((member) => member.userId === person.correlatedUserId);

  return (
    <form onSubmit={(event: Event) => void save(event)} aria-label="Correlate with a member">
      <Stack gap="sm">
        <p>
          {person.correlatedUserId ? (
            <>
              Correlated with{' '}
              <a href={organizationPath(`/members/${person.correlatedUserId}`)}>
                {memberLabel(current, person.correlatedUserId)}
              </a>
              .
            </>
          ) : (
            'Not correlated with any organization member.'
          )}
        </p>
        <label className="registration-field">
          <span>Organization member</span>
          <select value={userId()} onChange={(event: Event) => setUserId(inputValue(event))}>
            <option value="">No member (clear correlation)</option>
            {workforce.map((member) => (
              <option value={member.userId}>
                {memberLabel(member, member.userId)}
                {member.suspended ? ' (suspended)' : ''}
              </option>
            ))}
          </select>
        </label>
        <ActionError error={error()} noun="person" />
        {notice() ? <p role="status">{notice()}</p> : null}
        <Button variant="secondary" type="submit" disabled={pending()}>
          {pending() ? 'Saving…' : 'Save correlation'}
        </Button>
      </Stack>
    </form>
  );
}

export function PersonDetailPage({ personId }: { personId: string }) {
  const [version, setVersion] = state(0);
  const [saveNotice, setSaveNotice] = state<string | null>(null);
  const person = resource(() => getPerson(personId), [personId, version()]);

  if (person.pending && !person.value) {
    return (
      <Page>
        <Spinner label="Loading person" />
      </Page>
    );
  }

  if (person.error) {
    return (
      <RecordFailure
        error={person.error}
        noun="person"
        backPath="/workforce"
        backLabel="Back to workforce"
        onRetry={() => person.refresh()}
      />
    );
  }

  const current = person.value!;
  return (
    <Page>
      <PageHeader title={current.displayName} description={`Revision ${current.revision} · ${sourceKindLabel(current.sourceKind)}`} />
      <Stack gap="md">
        <a href={organizationPath('/workforce')}>Back to workforce</a>
        <Card>
          <CardHeader>
            <CardTitle>Edit person</CardTitle>
            <CardDescription>
              Last changed by {current.lastChangedBy} on {new Date(current.lastChangedAt).toLocaleString()}.
            </CardDescription>
          </CardHeader>
          <CardContent>
            {saveNotice() ? <p role="status">{saveNotice()}</p> : null}
            <PersonEditor
              key={`${current.personId}:${current.revision}`}
              person={current}
              onSaving={() => setSaveNotice(null)}
              onSaved={() => {
                setSaveNotice('Person saved.');
                setVersion(version() + 1);
              }}
            />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Organization member</CardTitle>
            <CardDescription>
              Correlation only links records for reconciliation. It never grants or revokes access.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <MembershipCorrelation person={current} onSaved={() => setVersion(version() + 1)} />
          </CardContent>
        </Card>
        <SourceObservationsPanel target={{ kind: 'person', id: current.personId, revision: current.revision, facts: { person: { display_name: current.displayName, work_email: current.workEmail } } }} />
      </Stack>
    </Page>
  );
}
