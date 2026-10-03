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

import { listTeams, type TeamSummary } from '../../teams/teams.js';
import { organizationPath } from '../../tenants/tenants.js';
import { emptyIdentity, expiryLabel, IdentityFields, reviewDateProblem } from '../identity-fields.js';
import {
  formatDate,
  identityKinds,
  identityLifecycles,
  listPeople,
  listServiceIdentities,
  optionLabel,
  recordServiceIdentity,
  unownedReasonLabel,
  WorkforceRequestError,
  type Person,
  type ServiceIdentityTerms,
} from '../workforce.js';
import { ActionError, LoadFailure, WorkforceForbidden, WorkforceSections } from '../workforce-shared.js';

function RecordIdentityForm({
  people,
  teams,
  onRecorded,
}: {
  people: Person[];
  teams: TeamSummary[];
  onRecorded: () => void;
}) {
  const [terms, setTerms] = state<ServiceIdentityTerms>({ ...emptyIdentity });
  const [pending, setPending] = state(false);
  const [error, setError] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  async function submit(event: Event) {
    event.preventDefault();
    setError(null);
    setNotice(null);
    const problem = reviewDateProblem(terms().reviewBy);
    if (problem) {
      setError(new Error(problem));
      return;
    }
    setPending(true);
    try {
      await recordServiceIdentity(terms());
      setNotice(`${terms().displayName.trim()} was recorded.`);
      setTerms({ ...emptyIdentity });
      onRecorded();
    } catch (failure) {
      setError(failure instanceof Error ? failure : new Error('Unable to record the service identity.'));
    } finally {
      setPending(false);
    }
  }

  return (
    <form onSubmit={(event: Event) => void submit(event)}>
      <Stack gap="sm">
        <IdentityFields terms={terms()} people={people} teams={teams} onChange={setTerms} />
        <ActionError error={error()} noun="service identity" />
        {notice() ? <p role="status">{notice()}</p> : null}
        <Button variant="primary" type="submit" disabled={pending()}>
          {pending() ? 'Recording…' : 'Record service identity'}
        </Button>
      </Stack>
    </form>
  );
}

export function ServiceIdentitiesPage() {
  const [version, setVersion] = state(0);
  const [unownedOnly, setUnownedOnly] = state(false);
  const identities = resource(() => listServiceIdentities(unownedOnly()), [unownedOnly(), version()]);
  const people = resource(() => listPeople(), [version()]);
  const teams = resource(() => listTeams().catch(() => [] as TeamSummary[]), [version()]);

  if (identities.error instanceof WorkforceRequestError && identities.error.status === 403) {
    return <WorkforceForbidden title="Service identities are not available to you" />;
  }

  const owners = new Map<string, string>([
    ...(people.value ?? []).map((person) => [person.personId, person.displayName] as [string, string]),
    ...(teams.value ?? []).map((team) => [team.teamId, team.name] as [string, string]),
  ]);
  const items = identities.value ?? [];

  return (
    <Page>
      <PageHeader
        title="Service identities"
        description="Every non-human identity needs an approved purpose, one accountable owner, and a review date."
      />
      <Stack gap="md">
        <WorkforceSections current="/workforce/service-identities" />
        <Card>
          <CardHeader>
            <CardTitle>Non-human identities</CardTitle>
            <CardDescription>An identity is unowned when its owner or review date no longer holds.</CardDescription>
          </CardHeader>
          <CardContent>
            <Stack gap="sm">
              <label className="workforce-toggle">
                <input
                  type="checkbox"
                  checked={unownedOnly()}
                  onChange={(event: Event) => setUnownedOnly((event.target as HTMLInputElement).checked)}
                />
                <span>Show only unowned identities</span>
              </label>
              {identities.pending && !identities.value ? (
                <Spinner label="Loading service identities" />
              ) : identities.error ? (
                <LoadFailure error={identities.error} onRetry={() => identities.refresh()} />
              ) : items.length === 0 ? (
                <p>{unownedOnly() ? 'Every service identity has an accountable owner.' : 'No service identities recorded yet.'}</p>
              ) : (
                <ul className="plain-list workforce-list">
                  {items.map((identity) => (
                    <li>
                      <a href={organizationPath(`/workforce/service-identities/${identity.serviceIdentityId}`)}>
                        {identity.displayName}
                      </a>{' '}
                      · {optionLabel(identityKinds, identity.identityKind)} ·{' '}
                      {optionLabel(identityLifecycles, identity.lifecycleStatus)} · owner{' '}
                      {owners.get(identity.ownerId) ?? `unknown ${identity.ownerKind}`} · review by{' '}
                      {formatDate(identity.reviewBy)} ·{' '}
                      {identity.expired ? (
                        <strong className="workforce-expired-tag">{expiryLabel(identity)}</strong>
                      ) : (
                        expiryLabel(identity)
                      )}
                      {identity.unowned ? (
                        <ul className="workforce-gaps" aria-label={`Why ${identity.displayName} is unowned`}>
                          {identity.unownedReasons.map((reason) => (
                            <li>{unownedReasonLabel(reason)}</li>
                          ))}
                        </ul>
                      ) : null}
                    </li>
                  ))}
                </ul>
              )}
            </Stack>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Record a service identity</CardTitle>
          </CardHeader>
          <CardContent>
            {people.pending && !people.value ? (
              <Spinner label="Loading owners" />
            ) : people.error ? (
              <LoadFailure error={people.error} onRetry={() => people.refresh()} />
            ) : (
              <RecordIdentityForm
                people={people.value ?? []}
                teams={teams.value ?? []}
                onRecorded={() => setVersion(version() + 1)}
              />
            )}
          </CardContent>
        </Card>
      </Stack>
    </Page>
  );
}
