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
  discardBoundaryDraft,
  getBoundary,
  getEffectiveBoundaryVersion,
  label,
  listBoundaryDecisions,
  listBoundaryVersions,
  ProgramRequestError,
  proposeBoundarySuccessor,
  reviseBoundaryDraft,
  type BoundaryContent,
} from '../boundaries.js';
import { BoundaryForm, describeBoundaryFailure } from '../components/boundary-form.js';
import { DecisionPanel } from '../components/decision-panel.js';
import { ResponsibilitiesPanel } from '../components/responsibilities-panel.js';
import { ScopeEntries } from '../components/scope-entries.js';
import { listAssignments, listMemberOptions, shortId, standingConflicts } from '../responsibilities.js';

export function BoundaryDetailPage({ programId, boundaryId }: { programId: string; boundaryId: string }) {
  const [version, setVersion] = state(0);
  const [written, setWritten] = state<number | undefined>(undefined);
  const [assignmentsWritten, setAssignmentsWritten] = state<number | undefined>(undefined);
  const [selectedVersionId, setSelectedVersionId] = state<string | null>(null);
  const [effectiveOn, setEffectiveOn] = state(new Date().toISOString().slice(0, 10));
  const [discardReason, setDiscardReason] = state('');
  const [discardError, setDiscardError] = state<Error | null>(null);
  const boundary = resource(() => getBoundary(boundaryId, written()), [boundaryId, version()]);
  const versions = resource(() => listBoundaryVersions(boundaryId), [boundaryId, version()]);
  const decisions = resource(() => listBoundaryDecisions(boundaryId), [boundaryId, version()]);
  const effective = resource(() => getEffectiveBoundaryVersion(boundaryId, effectiveOn()), [boundaryId, effectiveOn(), version()]);
  const members = resource(() => listMemberOptions(), []);
  const draftKey = boundary.value?.draft ? `${boundary.value.draft.versionId}:${boundary.value.draft.revision}` : '';
  const assignments = resource(async () => {
    const draft = boundary.value?.draft;
    if (!draft) return { revision: 0, assignments: [] };
    return listAssignments(
      { recordType: 'boundary', recordId: boundaryId, versionId: draft.versionId, revision: draft.revision },
      assignmentsWritten()
    );
  }, [boundaryId, draftKey, assignmentsWritten()]);

  const programPath = organizationPath(`/programs/${programId}/boundaries`);
  const exceptionsPath = organizationPath(`/programs/${programId}/boundaries/${boundaryId}/exceptions`);
  const back = <a href={programPath}>Back to boundaries</a>;

  if (boundary.pending && !boundary.value) {
    return (
      <Page>
        <Spinner label="Loading boundary" />
      </Page>
    );
  }

  if (boundary.error) {
    const error = boundary.error;
    const status = error instanceof ProgramRequestError ? error.status : null;
    return (
      <Page>
        <EmptyState
          title={status === 403 ? 'This boundary is not available to you' : status === 404 ? 'Boundary not found' : 'Boundary could not be loaded'}
          titleAs="h1"
          description={error.message}
          action={
            status === 403 || status === 404 ? (
              back
            ) : (
              <Button variant="primary" onPress={() => boundary.refresh()}>
                Try again
              </Button>
            )
          }
        />
      </Page>
    );
  }

  const current = boundary.value!;
  const draft = current.draft;
  const approved = current.approved;
  const refresh = (nextRevision?: number) => {
    setWritten(nextRevision);
    setVersion(version() + 1);
  };

  async function save(content: BoundaryContent) {
    await reviseBoundaryDraft(boundaryId, draft!.versionId, draft!.revision, content);
    refresh(current.revision + 1);
    return 'Boundary draft saved.';
  }

  async function propose(content: BoundaryContent) {
    await proposeBoundarySuccessor(boundaryId, approved!.versionId, content);
    refresh(current.revision + 1);
    return 'Successor draft proposed. Review its impact before approval.';
  }

  async function discard() {
    setDiscardError(null);
    try {
      await discardBoundaryDraft(boundaryId, draft!.versionId, draft!.revision, discardReason());
      refresh(current.revision + 1);
    } catch (failure) {
      setDiscardError(failure instanceof Error ? failure : new Error('Unable to discard the draft.'));
    }
  }

  const selected = (versions.value ?? []).find((item) => item.versionId === selectedVersionId());
  const decisionList = decisions.value ?? [];
  const title = (draft ?? approved)?.content.statement.slice(0, 80) || 'System boundary';

  return (
    <Page>
      <PageHeader
        title={title}
        description={`${draft ? `Draft revision ${draft.revision} by ${draft.author}` : 'No open draft'}${
          approved ? ` · approved revision ${approved.revision}${approved.effectiveFrom ? ` effective ${approved.effectiveFrom}` : ''}` : ' · never approved'
        }`}
      />
      <Stack gap="md">
        {back}
        <a href={exceptionsPath}>Separation-of-duties exceptions for this boundary</a>
        {draft ? (
          <>
            <Card>
              <CardHeader>
                <CardTitle>Edit draft revision {draft.revision}</CardTitle>
                <CardDescription>Saving checks that nobody else changed revision {draft.revision} since you opened it.</CardDescription>
              </CardHeader>
              <CardContent>
                <BoundaryForm initial={draft.content} submitLabel="Save draft" onSubmit={save} />
              </CardContent>
            </Card>
            <ResponsibilitiesPanel
              programId={programId}
              scope={{ recordType: 'boundary', recordId: boundaryId, versionId: draft.versionId, revision: draft.revision }}
              members={members.value ?? []}
              assignments={assignments}
              onChanged={(revision) => setAssignmentsWritten(revision)}
            />
            {decisions.pending && !decisions.value ? (
              <Spinner label="Loading decisions" />
            ) : (
              <DecisionPanel
                boundaryId={boundaryId}
                draft={draft}
                decisions={decisionList}
                conflicts={standingConflicts(assignments.value?.assignments ?? [])}
                exceptionsPath={exceptionsPath}
                onDecided={() => refresh(current.revision + 1)}
              />
            )}
            <Card>
              <CardHeader>
                <CardTitle>Discard draft</CardTitle>
              </CardHeader>
              <CardContent>
                <Stack gap="sm">
                  <label className="registration-field">
                    <span>Reason for discarding</span>
                    <input type="text" value={discardReason()} onInput={(event: Event) => setDiscardReason((event.target as HTMLInputElement).value)} />
                  </label>
                  {discardError() ? <p role="alert">{describeBoundaryFailure(discardError()!)}</p> : null}
                  <Button variant="secondary" disabled={discardReason().trim() === ''} onPress={() => void discard()}>
                    Discard draft revision {draft.revision}
                  </Button>
                </Stack>
              </CardContent>
            </Card>
          </>
        ) : approved ? (
          <Card>
            <CardHeader>
              <CardTitle>Propose a successor</CardTitle>
              <CardDescription>
                Approved versions are immutable. A successor starts from approved revision {approved.revision} and shows its
                impact before approval.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <BoundaryForm initial={approved.content} submitLabel="Propose successor draft" onSubmit={propose} />
            </CardContent>
          </Card>
        ) : null}
        <Card>
          <CardHeader>
            <CardTitle>Effective version</CardTitle>
            <CardDescription>The approved boundary in force on a given date.</CardDescription>
          </CardHeader>
          <CardContent>
            <Stack gap="sm">
              <label className="registration-field">
                <span>Effective on</span>
                <input type="date" value={effectiveOn()} onInput={(event: Event) => setEffectiveOn((event.target as HTMLInputElement).value)} />
              </label>
              {effective.pending && effective.value === undefined ? (
                <Spinner label="Loading effective version" />
              ) : effective.error ? (
                <Stack gap="sm">
                  <p role="alert">{effective.error.message}</p>
                  <Button variant="secondary" onPress={() => effective.refresh()}>
                    Try again
                  </Button>
                </Stack>
              ) : effective.value ? (
                <Stack gap="sm">
                  <p>
                    Revision {effective.value.revision}, effective {effective.value.effectiveFrom ?? 'unknown'}, authored by{' '}
                    {effective.value.author}.
                  </p>
                  <ScopeEntries content={effective.value.content} />
                </Stack>
              ) : (
                <p>No approved version is effective on this date.</p>
              )}
            </Stack>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Versions</CardTitle>
          </CardHeader>
          <CardContent>
            {versions.pending && !versions.value ? (
              <Spinner label="Loading versions" />
            ) : versions.error ? (
              <Stack gap="sm">
                <p role="alert">{versions.error.message}</p>
                <Button variant="secondary" onPress={() => versions.refresh()}>
                  Try again
                </Button>
              </Stack>
            ) : (
              <Stack gap="sm">
                <ol className="plain-list boundary-versions">
                  {(versions.value ?? []).map((item) => (
                    <li>
                      <Button
                        variant="secondary"
                        aria-pressed={item.versionId === selectedVersionId() ? 'true' : 'false'}
                        onPress={() => setSelectedVersionId(item.versionId === selectedVersionId() ? null : item.versionId)}
                      >
                        Revision {item.revision} · {label(item.status)}
                      </Button>{' '}
                      <span className="boundary-meta">
                        by {item.author} on {new Date(item.changedAt).toLocaleDateString()}
                        {item.effectiveFrom ? ` · effective ${item.effectiveFrom}` : ''}
                      </span>
                    </li>
                  ))}
                </ol>
                {selected ? (
                  <section aria-label={`Revision ${selected.revision}`}>
                    <h3>Revision {selected.revision}</h3>
                    <ScopeEntries content={selected.content} />
                  </section>
                ) : null}
              </Stack>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Decision history</CardTitle>
          </CardHeader>
          <CardContent>
            {decisions.error ? (
              <Stack gap="sm">
                <p role="alert">{decisions.error.message}</p>
                <Button variant="secondary" onPress={() => decisions.refresh()}>
                  Try again
                </Button>
              </Stack>
            ) : decisionList.length === 0 ? (
              <p>No decisions yet.</p>
            ) : (
              <ol className="plain-list boundary-decisions">
                {decisionList.map((decision) => (
                  <li>
                    <strong>{label(decision.outcome)}</strong> of revision {decision.revision} by {decision.actor} on{' '}
                    {new Date(decision.decidedAt).toLocaleString()}: {decision.rationale}
                    {decision.waiverId ? (
                      <>
                        {' '}
                        · under{' '}
                        <a href={`${exceptionsPath}?exception=${decision.waiverId}`}>exception {shortId(decision.waiverId)}</a>
                      </>
                    ) : null}
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
