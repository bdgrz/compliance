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
import {
  commitmentKinds,
  createCommitmentDraft,
  describeCommitmentFailure,
  label,
  listCommitmentDrafts,
  listServiceOptions,
  listSubserviceProviderOptions,
  ProgramRequestError,
} from '../commitments.js';

function inputValue(event: Event): string {
  return (event.target as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement).value;
}

export function CommitmentsPage({ programId }: { programId: string }) {
  const drafts = resource(() => listCommitmentDrafts(programId), [programId]);
  const services = resource(() => listServiceOptions(programId), [programId]);
  const [kind, setKind] = state<string>('service_commitment');
  const [serviceId, setServiceId] = state('');
  const [providerId, setProviderId] = state('');
  const [identifier, setIdentifier] = state('');
  const [statement, setStatement] = state('');
  const [context, setContext] = state('');
  const [sourceReference, setSourceReference] = state('');
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<Error | null>(null);
  const subserviceProviders = resource(
    () => kind() === 'subservice_responsibility' ? listSubserviceProviderOptions() : Promise.resolve([]),
    [kind()]
  );
  const back = <a href={organizationPath(`/programs/${programId}`)}>Back to program</a>;

  if (drafts.pending && !drafts.value) {
    return (
      <Page>
        <Spinner label="Loading commitments" />
      </Page>
    );
  }

  if (drafts.error) {
    const error = drafts.error;
    const status = error instanceof ProgramRequestError ? error.status : null;
    return (
      <Page>
        <EmptyState
          title={
            status === 403
              ? 'Commitments are not available to you'
              : status === 404
                ? 'Program not found'
                : 'Commitments could not be loaded'
          }
          titleAs="h1"
          description={error.message}
          action={
            status === 403 || status === 404 ? (
              back
            ) : (
              <Button variant="primary" onPress={() => drafts.refresh()}>
                Try again
              </Button>
            )
          }
        />
      </Page>
    );
  }

  const list = drafts.value ?? [];
  const serviceList = services.value ?? [];
  const providerList = subserviceProviders.value ?? [];
  const selectedService = serviceId() || serviceList[0]?.serviceId || '';
  const selectedProvider = providerList.find((provider) => provider.providerId === providerId()) ?? providerList[0];
  const requiresProvider = kind() === 'subservice_responsibility';

  async function create(event: Event) {
    event.preventDefault();
    setActionError(null);
    setPending(true);
    try {
      const draftId = await createCommitmentDraft(programId, {
        kind: kind(),
        serviceId: selectedService,
        identifier: identifier(),
        statement: statement(),
        context: context(),
        sourceReference: sourceReference(),
        providerId: requiresProvider ? selectedProvider?.providerId ?? null : null,
      });
      window.location.assign(organizationPath(`/programs/${programId}/commitments/${draftId}`));
    } catch (failure) {
      setActionError(failure instanceof Error ? failure : new Error('Unable to create the commitment.'));
    } finally {
      setPending(false);
    }
  }

  const error = actionError();

  return (
    <Page>
      <PageHeader
        title="Commitments and requirements"
        description="Record service commitments, system requirements, CUECs, and CSOCs with their source, then review and approve each version."
      />
      <Stack gap="md">
        {back}
        {commitmentKinds.map((group) => {
          const items = list.filter((item) => item.kind === group.value);
          return (
            <Card>
              <CardHeader>
                <CardTitle>{group.title}</CardTitle>
                <CardDescription>
                  {group.internal
                    ? 'Performed by the service organization.'
                    : 'Performed outside the service organization; never counted as an internally performed control.'}
                </CardDescription>
              </CardHeader>
              <CardContent>
                {items.length === 0 ? (
                  <p>None recorded yet.</p>
                ) : (
                  <ul className="plain-list commitment-list">
                    {items.map((item) => (
                      <li>
                        <a href={organizationPath(`/programs/${programId}/commitments/${item.draftId}`)}>{item.identifier}</a>{' '}
                        <span className="commitment-meta">
                          {item.statement.slice(0, 100)} · revision {item.revision} · {label(item.status)} · source{' '}
                          {label(item.sourceResolution)}
                          {item.kind === 'subservice_responsibility' && item.providerId ? (
                            <> · Subservice provider <a href={organizationPath(`/providers/${item.providerId}`)}>{item.providerId}</a></>
                          ) : null}
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>
          );
        })}
        <Card>
          <CardHeader>
            <CardTitle>Record a commitment or requirement</CardTitle>
            <CardDescription>
              The source reference locates the contract or document; a reviewer verifies it before approval.
            </CardDescription>
          </CardHeader>
          <CardContent>
            {services.error ? <p role="alert">{services.error.message}</p> : null}
            {services.value && serviceList.length === 0 ? (
              <p>Record an active client service for this program before adding commitments.</p>
            ) : null}
            <form className="commitment-form" onSubmit={(event: Event) => void create(event)}>
              <Stack gap="sm">
                <label className="registration-field">
                  <span>Kind</span>
                  <select value={kind()} onChange={(event: Event) => setKind(inputValue(event))}>
                    {commitmentKinds.map((item) => (
                      <option value={item.value} selected={item.value === kind()}>
                        {item.short}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="registration-field">
                  <span>Service</span>
                  <select value={selectedService} onChange={(event: Event) => setServiceId(inputValue(event))} required>
                    {serviceList.map((service) => (
                      <option value={service.serviceId} selected={service.serviceId === selectedService}>
                        {service.name}
                      </option>
                    ))}
                  </select>
                </label>
                {requiresProvider ? (
                  <div className="registration-field">
                    <label htmlFor="commitment-subservice-provider">Subservice provider</label>
                    {subserviceProviders.pending ? (
                      <p role="status">Loading subservice providers…</p>
                    ) : subserviceProviders.error ? (
                      <p role="alert">{subserviceProviders.error.message}</p>
                    ) : providerList.length === 0 ? (
                      <p>Record a provider as a subservice organization before adding its CSOCs.</p>
                    ) : (
                      <select
                        id="commitment-subservice-provider"
                        value={selectedProvider?.providerId ?? ''}
                        onChange={(event: Event) => setProviderId(inputValue(event))}
                        required
                      >
                        <option value="" disabled>Select a subservice provider</option>
                        {providerList.map((provider) => (
                          <option value={provider.providerId} selected={provider.providerId === selectedProvider?.providerId}>
                            {provider.name} · {provider.boundaryTreatment?.replaceAll('_', ' ') ?? 'treatment unresolved'}
                          </option>
                        ))}
                      </select>
                    )}
                  </div>
                ) : null}
                <label className="registration-field">
                  <span>Identifier</span>
                  <input type="text" value={identifier()} onInput={(event: Event) => setIdentifier(inputValue(event))} required />
                </label>
                <label className="registration-field">
                  <span>Statement</span>
                  <textarea rows={3} value={statement()} onInput={(event: Event) => setStatement(inputValue(event))} required />
                </label>
                <label className="registration-field">
                  <span>Context</span>
                  <textarea rows={2} value={context()} onInput={(event: Event) => setContext(inputValue(event))} />
                </label>
                <label className="registration-field">
                  <span>Source reference</span>
                  <input
                    type="text"
                    value={sourceReference()}
                    onInput={(event: Event) => setSourceReference(inputValue(event))}
                    required
                  />
                </label>
                {error ? <p role="alert">{describeCommitmentFailure(error)}</p> : null}
                <Button
                  variant="primary"
                  type="submit"
                  disabled={pending() || selectedService === '' ||
                    (requiresProvider && (subserviceProviders.pending || !!subserviceProviders.error || !selectedProvider))}
                >
                  {pending() ? 'Saving…' : 'Create draft'}
                </Button>
              </Stack>
            </form>
          </CardContent>
        </Card>
      </Stack>
    </Page>
  );
}
