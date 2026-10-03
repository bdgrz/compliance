import { state } from '@askrjs/askr';
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
import { listPeople, type Person } from '../../workforce/workforce.js';
import { cleanContent, emptyContent, ProviderFields } from '../components/provider-fields.js';
import { UnresolvedList } from '../components/provider-facts.js';
import { listProviders, messageFor, optionLabel, ProviderRequestError, recordProvider, type ProviderContent } from '../providers.js';

export function ProvidersPage() {
  const [content, setContent] = state<ProviderContent>({ ...emptyContent });
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<Error | null>(null);
  const providers = resource(() => listProviders(), []);
  const people = resource(() => listPeople().catch(() => [] as Person[]), []);

  async function record(event: Event) {
    event.preventDefault();
    setActionError(null);
    setPending(true);
    try {
      const providerId = await recordProvider(cleanContent(content()));
      window.location.assign(organizationPath(`/providers/${providerId}`));
    } catch (failure) {
      setActionError(failure instanceof Error ? failure : new Error('Unable to record the provider.'));
      setPending(false);
    }
  }

  if (providers.error instanceof ProviderRequestError && providers.error.status === 403) {
    return (
      <Page>
        <EmptyState
          title="The provider register is not available to you"
          titleAs="h1"
          description="Ask a Compliance Lead or Org Admin for access to this organization's provider register."
        />
      </Page>
    );
  }

  const items = providers.value ?? [];
  const ownerNames = new Map((people.value ?? []).map((person) => [person.personId, person.displayName]));
  const error = actionError();

  return (
    <Page>
      <PageHeader
        title="Providers"
        description="Vendors and subservice organizations the service relies on. Missing classification, owners, and sources stay visible as unresolved until someone records them."
      />
      <Stack gap="md">
        <Card>
          <CardHeader>
            <CardTitle>Provider register</CardTitle>
            <CardDescription>A register entry records authored facts. It is not an assurance conclusion or a scope approval.</CardDescription>
          </CardHeader>
          <CardContent>
            {providers.pending && !providers.value ? (
              <Spinner label="Loading providers" />
            ) : providers.error ? (
              <Stack gap="sm">
                <p role="alert">{providers.error.message}</p>
                <Button variant="secondary" onPress={() => providers.refresh()}>
                  Try again
                </Button>
              </Stack>
            ) : items.length === 0 ? (
              <p>No providers yet. Record the first one to start tracking third-party reliance.</p>
            ) : (
              <ul className="plain-list provider-list">
                {items.map((provider) => {
                  const facts = provider.content;
                  const owner = facts.ownerPersonId ? ownerNames.get(facts.ownerPersonId) ?? 'Person not on the roster' : null;
                  return (
                    <li>
                      <a href={organizationPath(`/providers/${provider.providerId}`)}>{facts.name}</a>
                      <span> · {facts.providerKind}</span>
                      <span> · {facts.materiality ? optionLabel(facts.materiality) : 'Materiality unresolved'}</span>
                      {facts.materialityBasis.length > 0 ? <span> ({facts.materialityBasis.map(optionLabel).join(', ')})</span> : null}
                      <span> · Owner: {owner ?? 'Owner unresolved'}</span>
                      {facts.subservice && facts.boundaryTreatment ? <span> · Subservice, {optionLabel(facts.boundaryTreatment)}</span> : null}
                      <span> · {facts.dependencies.length} {facts.dependencies.length === 1 ? 'dependency' : 'dependencies'}</span>
                      <UnresolvedList label={`Unresolved for ${facts.name}`} codes={provider.unresolved} />
                    </li>
                  );
                })}
              </ul>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Record a provider</CardTitle>
            <CardDescription>Only the name and kind are required. Everything else can be added later in a revision.</CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={(event: Event) => void record(event)} aria-label="Record a provider">
              <Stack gap="sm">
                <ProviderFields content={content()} onChange={setContent} />
                {error ? <p role="alert">{messageFor(error, 'the register')}</p> : null}
                <Button variant="primary" type="submit" disabled={pending()}>
                  {pending() ? 'Recording…' : 'Record provider'}
                </Button>
              </Stack>
            </form>
          </CardContent>
        </Card>
      </Stack>
    </Page>
  );
}
