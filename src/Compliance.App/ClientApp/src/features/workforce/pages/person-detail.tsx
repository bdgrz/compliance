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
import { getPerson, revisePerson, sourceKindLabel, type Person } from '../workforce.js';
import { ActionError, inputValue, RecordFailure } from '../workforce-shared.js';

function PersonEditor({ person, onSaved }: { person: Person; onSaved: () => void }) {
  const [name, setName] = state(person.displayName);
  const [email, setEmail] = state(person.workEmail ?? '');
  const [pending, setPending] = state(false);
  const [error, setError] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  async function save(event: Event) {
    event.preventDefault();
    setError(null);
    setNotice(null);
    setPending(true);
    try {
      await revisePerson(person.personId, person.revision, name(), email() || null);
      setNotice('Person saved.');
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
        {notice() ? <p role="status">{notice()}</p> : null}
        <Button variant="primary" type="submit" disabled={pending()}>
          {pending() ? 'Saving…' : 'Save person'}
        </Button>
      </Stack>
    </form>
  );
}

export function PersonDetailPage({ personId }: { personId: string }) {
  const [version, setVersion] = state(0);
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
            <PersonEditor person={current} onSaved={() => setVersion(version() + 1)} />
          </CardContent>
        </Card>
      </Stack>
    </Page>
  );
}
