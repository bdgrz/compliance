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
  previewProviderChange,
  ProviderRequestError,
  reviseProvider,
  type Provider,
  type ProviderChangeImpactPreview,
  type ProviderChangeKind,
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

function ProviderChangeImpactCard({ provider }: { provider: Provider }) {
  const [changeKind, setChangeKind] = state<ProviderChangeKind>('renewal');
  const [effectiveOn, setEffectiveOn] = state(new Date().toISOString().slice(0, 10));
  const [summary, setSummary] = state('');
  const [pending, setPending] = state(false);
  const [assessment, setAssessment] = state<ProviderChangeImpactPreview | null>(null);
  const [actionError, setActionError] = state<Error | null>(null);

  async function preview(event: Event) {
    event.preventDefault();
    setActionError(null);
    setAssessment(null);
    setPending(true);
    try {
      setAssessment(
        await previewProviderChange(provider.providerId, provider.revision, changeKind(), effectiveOn(), summary().trim())
      );
    } catch (failure) {
      setActionError(failure instanceof Error ? failure : new Error('Unable to preview this provider change.'));
    } finally {
      setPending(false);
    }
  }

  const error = actionError();
  const stale = error instanceof ProviderRequestError && error.status === 409 && !error.transient;
  const impact = assessment();

  return (
    <Card>
      <CardHeader>
        <CardTitle>Assess a provider change</CardTitle>
        <CardDescription>
          Review linked systems, data, controls, evidence, and scope before making a separate approval decision. This preview does not approve or record a lifecycle change.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Stack gap="sm">
          <form onSubmit={(event: Event) => void preview(event)} aria-label="Preview provider change impact">
            <Stack gap="sm">
              <label className="registration-field">
                <span>Change type</span>
                <select
                  value={changeKind()}
                  onChange={(event: Event) => {
                    setChangeKind((event.target as HTMLSelectElement).value as ProviderChangeKind);
                    setAssessment(null);
                  }}
                >
                  <option value="renewal">Renewal</option>
                  <option value="material_change">Material service change</option>
                  <option value="termination">Termination</option>
                </select>
              </label>
              <label className="registration-field">
                <span>Effective date</span>
                <input
                  type="date"
                  value={effectiveOn()}
                  onInput={(event: Event) => {
                    setEffectiveOn((event.target as HTMLInputElement).value);
                    setAssessment(null);
                  }}
                  required
                />
              </label>
              <label className="registration-field">
                <span>Change summary</span>
                <textarea
                  value={summary()}
                  onInput={(event: Event) => {
                    setSummary((event.target as HTMLTextAreaElement).value);
                    setAssessment(null);
                  }}
                  maxLength={2000}
                  required
                />
              </label>
              {error ? <p role="alert">{messageFor(error, 'this provider')}</p> : null}
              {stale ? <p>Reload the provider before requesting another assessment.</p> : null}
              <Button variant="primary" type="submit" disabled={pending()}>
                {pending() ? 'Assessing…' : 'Preview impact'}
              </Button>
            </Stack>
          </form>

          {pending() ? <Spinner label="Assessing provider change impact" /> : null}
          {impact && impact.providerRevision !== provider.revision ? (
            <p role="alert">
              This assessment belongs to provider revision {impact.providerRevision}; the current revision is {provider.revision}. Preview again before relying on it.
            </p>
          ) : null}
          {impact && impact.providerRevision === provider.revision ? (
            <section aria-label="Provider change impact results" aria-live="polite">
              <h3>{impact.complete ? 'Assessment complete' : 'Assessment needs follow-up'}</h3>
              <p>
                {optionLabel(impact.changeKind)} effective {formatDate(impact.effectiveOn)} · provider revision {impact.providerRevision}
              </p>
              {!impact.complete ? (
                <p role="alert">Some impact contexts are incomplete. Resolve the listed gaps before relying on this preview for approval.</p>
              ) : null}
              <Stack gap="sm">
                {impact.contexts.map((section) => (
                  <section aria-label={`${optionLabel(section.context)} impact`}>
                    <h4>
                      {optionLabel(section.context)} {section.complete ? '' : '· Follow-up required'}
                    </h4>
                    {section.incompleteReason ? <p role="alert">Incomplete: {optionLabel(section.incompleteReason)}</p> : null}
                    {section.records.length === 0 ? (
                      <p>No linked records found.</p>
                    ) : (
                      <ul>
                        {section.records.map((record) => (
                          <li>
                            <strong>{optionLabel(record.recordType)}</strong> · {optionLabel(record.relationship)} ·{' '}
                            <code>{record.recordId}</code>
                            {record.parentRecordId ? <> · Parent <code>{record.parentRecordId}</code></> : null}
                            {record.revision !== null ? ` · Revision ${record.revision}` : ''}
                          </li>
                        ))}
                      </ul>
                    )}
                  </section>
                ))}
              </Stack>
              <p>Assessment digest: <code>{impact.digest}</code></p>
            </section>
          ) : null}
        </Stack>
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
        <ProviderChangeImpactCard provider={current} />
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
