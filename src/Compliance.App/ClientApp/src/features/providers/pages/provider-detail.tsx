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

import { listApplications, type ApplicationSummary } from '../../applications/applications.js';
import { organizationPath } from '../../tenants/tenants.js';
import { listPeople, type Person } from '../../workforce/workforce.js';
import { CitationView, DependencyList, formatDate, ProviderFacts, UnresolvedList } from '../components/provider-facts.js';
import { cleanContent, ProviderFields } from '../components/provider-fields.js';
import {
  getProvider,
  listProviderRevisions,
  messageFor,
  optionLabel,
  ProviderRequestError,
  reviseProvider,
  type Provider,
  type ProviderContent,
} from '../providers.js';

// The form starts from the revision being read and saves against exactly that revision.
function ReviseCard({
  provider,
  onSaved,
  onReload,
}: {
  provider: Provider;
  onSaved: (revision: number) => void;
  onReload: () => void;
}) {
  const [content, setContent] = state<ProviderContent>(provider.content);
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<Error | null>(null);

  async function save(event: Event) {
    event.preventDefault();
    setActionError(null);
    setPending(true);
    try {
      onSaved(await reviseProvider(provider.providerId, provider.revision, cleanContent(content())));
    } catch (failure) {
      setActionError(failure instanceof Error ? failure : new Error('Unable to save the provider.'));
    } finally {
      setPending(false);
    }
  }

  const error = actionError();
  const stale = error instanceof ProviderRequestError && error.status === 409 && !error.transient;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Revise provider</CardTitle>
        <CardDescription>Each save appends a revision; earlier revisions are kept. This save is made against revision {provider.revision}.</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={(event: Event) => void save(event)} aria-label="Revise provider">
          <Stack gap="sm">
            <ProviderFields content={content()} onChange={setContent} />
            {error ? <p role="alert">{messageFor(error, 'this provider')}</p> : null}
            {stale ? (
              <Button variant="secondary" onPress={onReload}>
                Reload provider
              </Button>
            ) : null}
            <Button variant="primary" type="submit" disabled={pending()}>
              {pending() ? 'Saving…' : 'Save revision'}
            </Button>
          </Stack>
        </form>
      </CardContent>
    </Card>
  );
}

export function ProviderDetailPage({ providerId }: { providerId: string }) {
  const [version, setVersion] = state(0);
  const [minimumRevision, setMinimumRevision] = state<number | undefined>(undefined);
  const provider = resource(() => getProvider(providerId, minimumRevision()), [providerId, minimumRevision(), version()]);
  const history = resource(() => listProviderRevisions(providerId), [providerId, version()]);
  const people = resource(() => listPeople().catch(() => [] as Person[]), []);
  const applications = resource(() => listApplications().catch(() => [] as ApplicationSummary[]), []);
  const back = <a href={organizationPath('/providers')}>Back to providers</a>;
  const reload = (revision?: number) => {
    if (revision !== undefined) setMinimumRevision(revision);
    setVersion(version() + 1);
  };

  if (provider.pending && !provider.value) {
    return (
      <Page>
        <Spinner label="Loading provider" />
      </Page>
    );
  }

  if (provider.error) {
    const error = provider.error;
    const status = error instanceof ProviderRequestError ? error.status : null;
    return (
      <Page>
        <EmptyState
          title={
            status === 403 ? 'This provider is not available to you' : status === 404 ? 'Provider not found' : 'Provider could not be loaded'
          }
          titleAs="h1"
          description={error.message}
          action={
            status === 403 || status === 404 ? (
              back
            ) : (
              <Button variant="primary" onPress={() => provider.refresh()}>
                Try again
              </Button>
            )
          }
        />
      </Page>
    );
  }

  const current = provider.value!;
  const facts = current.content;
  const ownerName = facts.ownerPersonId ? (people.value ?? []).find((person) => person.personId === facts.ownerPersonId)?.displayName ?? null : null;
  const applicationNames = new Map((applications.value ?? []).map((application) => [application.applicationId, application.name]));

  return (
    <Page>
      <PageHeader title={facts.name} description={`${facts.providerKind} provider`} />
      <Stack gap="md">
        {back}
        <Card>
          <CardHeader>
            <CardTitle>Classification and ownership</CardTitle>
            <CardDescription>
              Revision {current.revision}. Recorded by {current.recordedBy} on {formatDate(current.recordedAt)} ({optionLabel(current.sourceKind)} entry).
              Authored facts only: not an assurance conclusion or a scope approval.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <ProviderFacts provider={current} ownerName={ownerName} />
            <UnresolvedList label="Unresolved" codes={current.unresolved} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Source reference</CardTitle>
            <CardDescription>Citation metadata only. Cited content is never shown or fetched here.</CardDescription>
          </CardHeader>
          <CardContent>
            {facts.sourceCitation ? <CitationView citation={facts.sourceCitation} /> : <p>No source citation recorded. It remains unresolved.</p>}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Services and systems</CardTitle>
            <CardDescription>Declared dependencies on registered client services and system instances.</CardDescription>
          </CardHeader>
          <CardContent>
            <DependencyList dependencies={facts.dependencies} applicationNames={applicationNames} />
          </CardContent>
        </Card>
        {provider.pending ? (
          <Spinner label="Refreshing provider" />
        ) : (
          <ReviseCard provider={current} onSaved={(revision) => reload(revision)} onReload={() => reload()} />
        )}
        <Card>
          <CardHeader>
            <CardTitle>Revision history</CardTitle>
          </CardHeader>
          <CardContent>
            {history.pending && !history.value ? (
              <Spinner label="Loading history" />
            ) : history.error ? (
              <Stack gap="sm">
                <p role="alert">{history.error.message}</p>
                <Button variant="secondary" onPress={() => history.refresh()}>
                  Try again
                </Button>
              </Stack>
            ) : (
              <ol className="plain-list provider-history">
                {(history.value ?? []).map((revision) => (
                  <li>
                    <strong>Revision {revision.revision}</strong> by {revision.recordedBy} on {formatDate(revision.recordedAt)} · {revision.content.name} ·{' '}
                    {revision.content.materiality ? optionLabel(revision.content.materiality) : 'Materiality unresolved'}
                    {revision.content.subservice && revision.content.boundaryTreatment ? ` · ${optionLabel(revision.content.boundaryTreatment)}` : ''}
                    {` · ${revision.content.dependencies.length} dependencies`}
                    <UnresolvedList label={`Unresolved in revision ${revision.revision}`} codes={revision.unresolved} />
                  </li>
                ))}
              </ol>
            )}
          </CardContent>
        </Card>
      </Stack>
    </Page>
  );
}
