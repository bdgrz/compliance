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
import { expiryLabel, IdentityFields, reviewDateProblem } from '../identity-fields.js';
import {
  formatDate,
  getServiceIdentity,
  identityKinds,
  identityLifecycles,
  listPeople,
  optionLabel,
  reviseServiceIdentity,
  sourceKindLabel,
  unownedReasonLabel,
  type Person,
  type ServiceIdentity,
  type ServiceIdentityTerms,
} from '../workforce.js';
import { ActionError, inputValue, RecordFailure } from '../workforce-shared.js';
import { SourceObservationsPanel } from '../source-record.js';

function IdentityEditor({
  identity,
  people,
  teams,
  onSaved,
}: {
  identity: ServiceIdentity;
  people: Person[];
  teams: TeamSummary[];
  onSaved: () => void;
}) {
  const [terms, setTerms] = state<ServiceIdentityTerms>({
    displayName: identity.displayName,
    identityKind: identity.identityKind,
    purpose: identity.purpose,
    environment: identity.environment,
    ownerKind: identity.ownerKind,
    ownerId: identity.ownerId,
    reviewBy: identity.reviewBy.slice(0, 10),
    expiresOn: identity.expiresOn ? identity.expiresOn.slice(0, 10) : null,
  });
  const [lifecycle, setLifecycle] = state(identity.lifecycleStatus);
  const [pending, setPending] = state(false);
  const [error, setError] = state<Error | null>(null);
  const [notice, setNotice] = state<string | null>(null);

  async function save(event: Event) {
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
      await reviseServiceIdentity(identity.serviceIdentityId, identity.revision, terms(), lifecycle());
      setNotice('Service identity saved.');
      onSaved();
    } catch (failure) {
      setError(failure instanceof Error ? failure : new Error('Unable to save this service identity.'));
    } finally {
      setPending(false);
    }
  }

  return (
    <form onSubmit={(event: Event) => void save(event)}>
      <Stack gap="sm">
        <IdentityFields terms={terms()} people={people} teams={teams} onChange={setTerms} />
        <label className="registration-field">
          <span>Lifecycle</span>
          <select value={lifecycle()} onChange={(event: Event) => setLifecycle(inputValue(event))} required>
            {identityLifecycles.map((option) => (
              <option value={option.value}>{option.label}</option>
            ))}
          </select>
        </label>
        <ActionError error={error()} noun="service identity" />
        {notice() ? <p role="status">{notice()}</p> : null}
        <Button variant="primary" type="submit" disabled={pending()}>
          {pending() ? 'Saving…' : 'Save service identity'}
        </Button>
      </Stack>
    </form>
  );
}

export function ServiceIdentityDetailPage({ serviceIdentityId }: { serviceIdentityId: string }) {
  const [version, setVersion] = state(0);
  const identity = resource(() => getServiceIdentity(serviceIdentityId), [serviceIdentityId, version()]);
  const people = resource(() => listPeople(), [version()]);
  const teams = resource(() => listTeams().catch(() => [] as TeamSummary[]), [version()]);

  if (identity.pending && !identity.value) {
    return (
      <Page>
        <Spinner label="Loading service identity" />
      </Page>
    );
  }

  if (identity.error) {
    return (
      <RecordFailure
        error={identity.error}
        noun="service identity"
        backPath="/workforce/service-identities"
        backLabel="Back to service identities"
        onRetry={() => identity.refresh()}
      />
    );
  }

  const current = identity.value!;
  return (
    <Page>
      <PageHeader
        title={current.displayName}
        description={`${optionLabel(identityKinds, current.identityKind)} · ${optionLabel(identityLifecycles, current.lifecycleStatus)} · ${expiryLabel(current)} · revision ${current.revision}`}
      />
      <Stack gap="md">
        <a href={organizationPath('/workforce/service-identities')}>Back to service identities</a>
        {current.expired ? (
          <p className="workforce-callout workforce-expired" role="note">
            This identity expired on {formatDate(current.expiresOn)}. Disable or retire it, or record a new expiry
            date if its credential was renewed.
          </p>
        ) : null}
        {current.unowned ? (
          <Card>
            <CardHeader>
              <CardTitle>Ownership gap</CardTitle>
              <CardDescription>This identity has no accountable owner until these are resolved.</CardDescription>
            </CardHeader>
            <CardContent>
              <ul className="workforce-gaps">
                {current.unownedReasons.map((reason) => (
                  <li>{unownedReasonLabel(reason)}</li>
                ))}
              </ul>
            </CardContent>
          </Card>
        ) : null}
        <Card>
          <CardHeader>
            <CardTitle>Revise service identity</CardTitle>
            <CardDescription>
              {sourceKindLabel(current.sourceKind)}. Last changed by {current.lastChangedBy} on{' '}
              {new Date(current.lastChangedAt).toLocaleString()}.
            </CardDescription>
          </CardHeader>
          <CardContent>
            {people.pending && !people.value ? (
              <Spinner label="Loading owners" />
            ) : (
              <IdentityEditor
                identity={current}
                people={people.value ?? []}
                teams={teams.value ?? []}
              onSaved={() => setVersion(version() + 1)}
              />
            )}
          </CardContent>
        </Card>
        <SourceObservationsPanel target={{ kind: 'service_identity', id: current.serviceIdentityId, revision: current.revision, facts: { service_identity: {
          display_name: current.displayName, identity_kind: current.identityKind, environment: current.environment,
          lifecycle_status: current.lifecycleStatus, expires_on: current.expiresOn,
        } } }} />
      </Stack>
    </Page>
  );
}
