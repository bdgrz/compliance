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
  ApplicationRequestError,
  codeLabel,
  declareSystemInstance,
  getApplication,
  listApplicationRevisions,
  listApplications,
  listSystemInstances,
  previewApplicationChange,
  retireApplication,
  reviseApplication,
  type Application,
  type ApplicationContent,
  type ApplicationSummary,
  type ChangePreview,
} from '../applications.js';
import { listPeople, type Person } from '../../workforce/workforce.js';

function OwnerName({ personId, names }: { personId: string | null; names: Map<string, string> }) {
  if (!personId) return <>Not set</>;
  const name = names.get(personId);
  return name ? (
    <a href={organizationPath(`/workforce/people/${personId}`)}>{name}</a>
  ) : (
    <>Person not on the roster</>
  );
}
import { AccessReviewScopePanel } from '../components/access-review-scope-panel.js';
import { ApplicationFields, cleanContent } from '../components/application-fields.js';
import { ChangePreviewView } from '../components/change-preview.js';
import { messageFor, startOfDay, today } from '../components/messages.js';

function contentOf(application: Application): ApplicationContent {
  return {
    name: application.name,
    purpose: application.purpose,
    ownerReference: application.ownerReference,
    classification: application.classification,
    systemOwnerPersonId: application.systemOwnerPersonId,
    accessOwnerPersonId: application.accessOwnerPersonId,
  };
}

// Revising previews the change against the expected revision first, then saves that same revision.
function ReviseCard({ application, onSaved }: { application: Application; onSaved: (revision: number) => void }) {
  const [content, setContent] = state<ApplicationContent>(contentOf(application));
  const [preview, setPreview] = state<ChangePreview | null>(null);
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<Error | null>(null);

  async function runPreview(event: Event) {
    event.preventDefault();
    setActionError(null);
    setPending(true);
    try {
      setPreview(await previewApplicationChange(application.applicationId, application.revision, { kind: 'revise', content: cleanContent(content()) }));
    } catch (failure) {
      setActionError(failure instanceof Error ? failure : new Error('Unable to preview the change.'));
    } finally {
      setPending(false);
    }
  }

  async function save() {
    setActionError(null);
    setPending(true);
    try {
      await reviseApplication(application.applicationId, application.revision, cleanContent(content()));
      setPreview(null);
      onSaved(application.revision + 1);
    } catch (failure) {
      setActionError(failure instanceof Error ? failure : new Error('Unable to save the application.'));
    } finally {
      setPending(false);
    }
  }

  const error = actionError();
  const ready = preview();

  return (
    <Card>
      <CardHeader>
        <CardTitle>Revise application</CardTitle>
        <CardDescription>Preview the impact before saving. Changes save against revision {application.revision}.</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={(event: Event) => void runPreview(event)} aria-label="Revise application">
          <Stack gap="sm">
            <ApplicationFields
              content={content()}
              onChange={(next) => {
                setContent(next);
                setPreview(null);
              }}
            />
            {error ? <p role="alert">{messageFor(error, 'this application')}</p> : null}
            <Button variant="secondary" type="submit" disabled={pending()}>
              {pending() && !ready ? 'Previewing…' : 'Preview change'}
            </Button>
          </Stack>
        </form>
        {ready ? (
          <Stack gap="sm">
            <ChangePreviewView preview={ready} />
            <Button variant="primary" onPress={() => void save()} disabled={pending()}>
              {pending() ? 'Saving…' : 'Save revision'}
            </Button>
          </Stack>
        ) : null}
      </CardContent>
    </Card>
  );
}

function RetireCard({ application, onRetired }: { application: Application; onRetired: () => void }) {
  const [reason, setReason] = state('');
  const [mergedInto, setMergedInto] = state('');
  const candidates = resource(() => listApplications(), []);
  const [effectiveAt, setEffectiveAt] = state(today());
  const [preview, setPreview] = state<ChangePreview | null>(null);
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<Error | null>(null);

  async function runPreview(event: Event) {
    event.preventDefault();
    setActionError(null);
    setPending(true);
    try {
      setPreview(await previewApplicationChange(application.applicationId, application.revision, { kind: 'retire' }));
    } catch (failure) {
      setActionError(failure instanceof Error ? failure : new Error('Unable to preview the retirement.'));
    } finally {
      setPending(false);
    }
  }

  async function retire() {
    setActionError(null);
    setPending(true);
    try {
      await retireApplication(
        application.applicationId,
        application.revision,
        startOfDay(effectiveAt()),
        reason().trim(),
        mergedInto() || null
      );
      onRetired();
    } catch (failure) {
      setActionError(failure instanceof Error ? failure : new Error('Unable to retire the application.'));
    } finally {
      setPending(false);
    }
  }

  const error = actionError();
  const ready = preview();

  return (
    <Card>
      <CardHeader>
        <CardTitle>Retire application</CardTitle>
        <CardDescription>Retirement keeps every revision, system instance, and scope decision.</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={(event: Event) => void runPreview(event)} aria-label="Retire application">
          <Stack gap="sm">
            <label className="registration-field">
              <span>Reason for retirement</span>
              <input type="text" value={reason()} onInput={(event: Event) => setReason((event.target as HTMLInputElement).value)} required />
            </label>
            <label className="registration-field">
              <span>Effective date</span>
              <input
                type="date"
                value={effectiveAt()}
                onInput={(event: Event) => setEffectiveAt((event.target as HTMLInputElement).value)}
                required
              />
            </label>
            <label className="registration-field">
              <span>Merged into (optional)</span>
              <select value={mergedInto()} onChange={(event: Event) => setMergedInto((event.target as HTMLSelectElement).value)}>
                <option value="">Not merged; retired outright</option>
                {(candidates.value ?? [])
                  .filter((other) => other.applicationId !== application.applicationId && other.lifecycle !== 'retired')
                  .map((other) => (
                    <option value={other.applicationId}>{other.name}</option>
                  ))}
              </select>
              <small>
                {candidates.error
                  ? 'Other applications could not be loaded, so a merge target cannot be chosen.'
                  : 'Choose the active application that replaces this one when they were consolidated.'}
              </small>
            </label>
            {error ? <p role="alert">{messageFor(error, 'this application')}</p> : null}
            <Button variant="secondary" type="submit" disabled={pending()}>
              Preview retirement
            </Button>
          </Stack>
        </form>
        {ready ? (
          <Stack gap="sm">
            <ChangePreviewView preview={ready} />
            <Button variant="primary" onPress={() => void retire()} disabled={pending()}>
              {pending() ? 'Retiring…' : 'Retire application'}
            </Button>
          </Stack>
        ) : null}
      </CardContent>
    </Card>
  );
}

function DeclareInstanceForm({ application, onDeclared }: { application: Application; onDeclared: () => void }) {
  const [name, setName] = state('');
  const [kind, setKind] = state('');
  const [boundary, setBoundary] = state('');
  const [pending, setPending] = state(false);
  const [actionError, setActionError] = state<Error | null>(null);

  async function declare(event: Event) {
    event.preventDefault();
    setActionError(null);
    setPending(true);
    try {
      await declareSystemInstance(application.applicationId, application.revision, {
        name: name().trim(),
        kind: kind().trim(),
        accessBoundaryReference: boundary().trim() || null,
      });
      setName('');
      setKind('');
      setBoundary('');
      onDeclared();
    } catch (failure) {
      setActionError(failure instanceof Error ? failure : new Error('Unable to record the system instance.'));
    } finally {
      setPending(false);
    }
  }

  const error = actionError();
  return (
    <form onSubmit={(event: Event) => void declare(event)} aria-label="Record a reviewed system">
      <Stack gap="sm">
        <label className="registration-field">
          <span>System name</span>
          <input type="text" value={name()} onInput={(event: Event) => setName((event.target as HTMLInputElement).value)} required />
        </label>
        <label className="registration-field">
          <span>Kind</span>
          <input type="text" value={kind()} onInput={(event: Event) => setKind((event.target as HTMLInputElement).value)} required />
          <small>For example production tenant, database, or admin console.</small>
        </label>
        <label className="registration-field">
          <span>Access boundary (optional)</span>
          <input type="text" value={boundary()} onInput={(event: Event) => setBoundary((event.target as HTMLInputElement).value)} />
        </label>
        {error ? <p role="alert">{messageFor(error, 'this application')}</p> : null}
        <Button variant="primary" type="submit" disabled={pending()}>
          {pending() ? 'Recording…' : 'Record reviewed system'}
        </Button>
      </Stack>
    </form>
  );
}

export function ApplicationDetailPage({ applicationId }: { applicationId: string }) {
  const [version, setVersion] = state(0);
  const [minimumRevision, setMinimumRevision] = state<number | undefined>(undefined);
  const [selected, setSelected] = state<string | null>(null);
  const application = resource(() => getApplication(applicationId, minimumRevision()), [applicationId, minimumRevision(), version()]);
  const instances = resource(() => listSystemInstances(applicationId), [applicationId, version()]);
  const history = resource(() => listApplicationRevisions(applicationId), [applicationId, version()]);
  const people = resource(() => listPeople().catch(() => [] as Person[]), []);
  const others = resource(() => listApplications().catch(() => [] as ApplicationSummary[]), [version()]);
  const back = <a href={organizationPath('/applications')}>Back to applications</a>;
  const reload = (revision?: number) => {
    if (revision !== undefined) setMinimumRevision(revision);
    setVersion(version() + 1);
  };

  if (application.pending && !application.value) {
    return (
      <Page>
        <Spinner label="Loading application" />
      </Page>
    );
  }

  if (application.error) {
    const error = application.error;
    const status = error instanceof ApplicationRequestError ? error.status : null;
    return (
      <Page>
        <EmptyState
          title={
            status === 403
              ? 'This application is not available to you'
              : status === 404
                ? 'Application not found'
                : 'Application could not be loaded'
          }
          titleAs="h1"
          description={error.message}
          action={
            status === 403 || status === 404 ? (
              back
            ) : (
              <Button variant="primary" onPress={() => application.refresh()}>
                Try again
              </Button>
            )
          }
        />
      </Page>
    );
  }

  const current = application.value!;
  const retired = current.lifecycle === 'retired';
  const systems = instances.value ?? [];
  const chosen = systems.find((instance) => instance.systemInstanceId === selected());
  const ownerNames = new Map((people.value ?? []).map((person) => [person.personId, person.displayName]));
  const mergeTargetId = current.retirement?.mergedIntoApplicationId ?? null;
  const mergeTargetName = mergeTargetId
    ? (others.value ?? []).find((other) => other.applicationId === mergeTargetId)?.name
    : undefined;

  return (
    <Page>
      <PageHeader title={current.name} description={current.purpose} />
      <Stack gap="md">
        {back}
        <Card>
          <CardHeader>
            <CardTitle>Ownership and classification</CardTitle>
            <CardDescription>
              Revision {current.revision}, last changed by {current.lastChangedBy} on{' '}
              {new Date(current.lastChangedAt).toLocaleDateString()}.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <dl className="application-facts">
              <dt>Owner</dt>
              <dd>{current.ownerReference ?? 'Unresolved'}</dd>
              <dt>Classification</dt>
              <dd>{current.classification ?? 'Unresolved'}</dd>
              <dt>System owner</dt>
              <dd>
                <OwnerName personId={current.systemOwnerPersonId} names={ownerNames} />
              </dd>
              <dt>Access owner</dt>
              <dd>
                <OwnerName personId={current.accessOwnerPersonId} names={ownerNames} />
              </dd>
            </dl>
            {current.unresolved.length > 0 ? (
              <ul className="application-unresolved" aria-label="Unresolved">
                {current.unresolved.map((code) => (
                  <li>{codeLabel(code)}</li>
                ))}
              </ul>
            ) : null}
            {retired && current.retirement ? (
              <p role="note" className="application-retired">
                Retired effective {new Date(current.retirement.effectiveAt).toLocaleDateString()}: {current.retirement.reason}
                {current.retirement.mergedIntoApplicationId ? (
                  <>
                    {' '}
                    Merged into{' '}
                    <a href={organizationPath(`/applications/${current.retirement.mergedIntoApplicationId}`)}>
                      {mergeTargetName ?? 'its successor application'}
                    </a>
                    .
                  </>
                ) : null}
              </p>
            ) : null}
          </CardContent>
        </Card>
        {retired ? null : <ReviseCard application={current} onSaved={(revision) => reload(revision)} />}
        <Card>
          <CardHeader>
            <CardTitle>Reviewed systems</CardTitle>
            <CardDescription>The system instances whose access is reviewed for this application.</CardDescription>
          </CardHeader>
          <CardContent>
            <Stack gap="sm">
              {instances.pending && !instances.value ? (
                <Spinner label="Loading reviewed systems" />
              ) : instances.error ? (
                <Stack gap="sm">
                  <p role="alert">{instances.error.message}</p>
                  <Button variant="secondary" onPress={() => instances.refresh()}>
                    Try again
                  </Button>
                </Stack>
              ) : systems.length === 0 ? (
                <p>No reviewed systems yet.</p>
              ) : (
                <ul className="plain-list system-instances">
                  {systems.map((instance) => (
                    <li>
                      <strong>{instance.name}</strong> ({instance.kind})
                      {instance.lifecycle === 'retired' ? <span className="application-tag"> Retired</span> : null}
                      {instance.accessBoundaryReference ? ` · ${instance.accessBoundaryReference}` : ''} · recorded by{' '}
                      {instance.declaredBy}
                      {instance.unresolved.length > 0 ? ` · ${instance.unresolved.map(codeLabel).join(', ')}` : ''}{' '}
                      <Button
                        variant="secondary"
                        onPress={() => setSelected(instance.systemInstanceId)}
                        aria-pressed={selected() === instance.systemInstanceId ? 'true' : 'false'}
                      >
                        {`Access-review scope for ${instance.name}`}
                      </Button>
                    </li>
                  ))}
                </ul>
              )}
              {retired ? null : <DeclareInstanceForm application={current} onDeclared={() => reload(current.revision + 1)} />}
            </Stack>
          </CardContent>
        </Card>
        {chosen ? (
          <AccessReviewScopePanel applicationId={applicationId} instance={chosen} onChanged={() => reload()} />
        ) : null}
        {retired ? null : <RetireCard application={current} onRetired={() => reload()} />}
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
              <ol className="plain-list application-history">
                {(history.value ?? []).map((revision) => (
                  <li>
                    <strong>Revision {revision.revision}</strong> ({codeLabel(revision.changeKind)}) by {revision.actor} on{' '}
                    {new Date(revision.changedAt).toLocaleDateString()} · {revision.name}: {revision.purpose} · Owner:{' '}
                    {revision.ownerReference ?? 'unresolved'}
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
