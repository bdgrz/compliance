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

import { ResponsibilitiesPanel } from '../../boundaries/components/responsibilities-panel.js';
import { listAssignments, listMemberOptions, shortId, standingConflicts } from '../../boundaries/responsibilities.js';
import { organizationPath } from '../../tenants/tenants.js';
import {
  describeCommitmentFailure,
  getCommitmentDraft,
  getEffectiveCommitmentVersion,
  isInternal,
  kindTitle,
  label,
  listCommitmentDecisions,
  listCommitmentRevisions,
  listCommitmentVersions,
  pendingAuthors,
  ProgramRequestError,
  reviseCommitmentDraft,
  type CommitmentVersion,
} from '../commitments.js';
import { CommitmentDecisionPanel } from '../components/commitment-decision-panel.js';

function inputValue(event: Event): string {
  return (event.target as HTMLInputElement | HTMLTextAreaElement).value;
}

function VersionSummary({ version }: { version: CommitmentVersion }) {
  return (
    <dl className="commitment-version">
      <dt>Statement</dt>
      <dd>{version.statement}</dd>
      <dt>Owner</dt>
      <dd>{version.ownerReference}</dd>
      <dt>Applicability</dt>
      <dd>{label(version.applicability)}</dd>
      <dt>Interpretation</dt>
      <dd>
        {label(version.interpretation)}
        {version.interpretationNote ? ` · ${version.interpretationNote}` : ''}
      </dd>
      <dt>Performed by</dt>
      <dd>
        {label(version.performedBy)}
        {version.internallyPerformed ? '' : ' (not an internally performed control)'}
      </dd>
      <dt>Source</dt>
      <dd>
        {version.sourceReference} · {label(version.sourceResolution)}
        {version.sourceEvidence ? ` · ${version.sourceEvidence}` : ''}
      </dd>
      <dt>Decisions</dt>
      <dd>
        Reviewed by {version.reviewedBy}
        {version.approvedBy ? `, approved by ${version.approvedBy}` : ' (recorded before separate approval)'}
      </dd>
    </dl>
  );
}

export function CommitmentDetailPage({ programId, draftId }: { programId: string; draftId: string }) {
  const [version, setVersion] = state(0);
  const [written, setWritten] = state<number | undefined>(undefined);
  const [assignmentsWritten, setAssignmentsWritten] = state<number | undefined>(undefined);
  const [selectedRevision, setSelectedRevision] = state<number | null>(null);
  const [effectiveOn, setEffectiveOn] = state(new Date().toISOString().slice(0, 10));
  const [statement, setStatement] = state<string | null>(null);
  const [context, setContext] = state<string | null>(null);
  const [sourceReference, setSourceReference] = state<string | null>(null);
  const [saving, setSaving] = state(false);
  const [saveError, setSaveError] = state<Error | null>(null);
  const [saveNotice, setSaveNotice] = state<string | null>(null);
  const draft = resource(() => getCommitmentDraft(programId, draftId, written()), [programId, draftId, version()]);
  const revisions = resource(() => listCommitmentRevisions(programId, draftId), [programId, draftId, version()]);
  const versions = resource(() => listCommitmentVersions(programId, draftId), [programId, draftId, version()]);
  const decisions = resource(() => listCommitmentDecisions(programId, draftId), [programId, draftId, version()]);
  const effective = resource(
    () => getEffectiveCommitmentVersion(programId, draftId, effectiveOn()),
    [programId, draftId, effectiveOn(), version()]
  );
  const members = resource(() => listMemberOptions(), []);
  const revisionKey = draft.value ? `${draft.value.revision}:${draft.value.status}` : '';
  const assignments = resource(async () => {
    const current = draft.value;
    if (!current || current.status === 'effective') return { revision: 0, assignments: [] };
    return listAssignments(
      { recordType: 'commitment', recordId: draftId, versionId: draftId, revision: current.revision },
      assignmentsWritten()
    );
  }, [draftId, revisionKey, assignmentsWritten()]);

  const listPath = organizationPath(`/programs/${programId}/commitments`);
  const exceptionsPath = organizationPath(`/programs/${programId}/commitments/${draftId}/exceptions`);
  const back = <a href={listPath}>Back to commitments</a>;

  if (draft.pending && !draft.value) {
    return (
      <Page>
        <Spinner label="Loading commitment" />
      </Page>
    );
  }

  if (draft.error) {
    const error = draft.error;
    const status = error instanceof ProgramRequestError ? error.status : null;
    return (
      <Page>
        <EmptyState
          title={
            status === 403
              ? 'This commitment is not available to you'
              : status === 404
                ? 'Commitment not found'
                : 'Commitment could not be loaded'
          }
          titleAs="h1"
          description={error.message}
          action={
            status === 403 || status === 404 ? (
              back
            ) : (
              <Button variant="primary" onPress={() => draft.refresh()}>
                Try again
              </Button>
            )
          }
        />
      </Page>
    );
  }

  const current = draft.value!;
  const refresh = (nextRevision?: number) => {
    setWritten(nextRevision);
    setStatement(null);
    setContext(null);
    setSourceReference(null);
    setVersion(version() + 1);
  };

  async function save(event: Event) {
    event.preventDefault();
    setSaveError(null);
    setSaveNotice(null);
    setSaving(true);
    try {
      await reviseCommitmentDraft(programId, draftId, current.revision, {
        statement: statement() ?? current.statement,
        context: context() ?? current.context,
        sourceReference: sourceReference() ?? current.sourceReference,
      });
      refresh(current.revision + 1);
      setSaveNotice('Draft saved. Any accepted review of the earlier revision no longer applies.');
    } catch (failure) {
      setSaveError(failure instanceof Error ? failure : new Error('Unable to save the commitment.'));
    } finally {
      setSaving(false);
    }
  }

  const revisionList = revisions.value ?? [];
  const versionList = versions.value ?? [];
  const decisionList = decisions.value ?? [];
  const selected = revisionList.find((item) => item.revision === selectedRevision());
  const pendingDecision = current.status !== 'effective';
  const internal = isInternal(current.kind);

  return (
    <Page>
      <PageHeader
        title={`${kindTitle(current.kind)} ${current.identifier}`}
        description={`Revision ${current.revision} · ${label(current.status)} · source ${label(current.sourceResolution)} · last changed by ${current.lastChangedBy}`}
      />
      <Stack gap="md">
        {back}
        {internal ? null : (
          <p className="commitment-carve-out">
            Performed outside the service organization. It is never counted as an internally performed control.
          </p>
        )}
        <Card>
          <CardHeader>
            <CardTitle>Edit revision {current.revision}</CardTitle>
            <CardDescription>
              Saving checks that nobody else changed revision {current.revision} since you opened it. Kind, identifier, and service
              do not change.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form className="commitment-form" onSubmit={(event: Event) => void save(event)}>
              <Stack gap="sm">
                <label className="registration-field">
                  <span>Statement</span>
                  <textarea
                    rows={3}
                    value={statement() ?? current.statement}
                    onInput={(event: Event) => setStatement(inputValue(event))}
                    required
                  />
                </label>
                <label className="registration-field">
                  <span>Context</span>
                  <textarea rows={2} value={context() ?? current.context} onInput={(event: Event) => setContext(inputValue(event))} />
                </label>
                <label className="registration-field">
                  <span>Source reference</span>
                  <input
                    type="text"
                    value={sourceReference() ?? current.sourceReference}
                    onInput={(event: Event) => setSourceReference(inputValue(event))}
                    required
                  />
                </label>
                {saveError() ? <p role="alert">{describeCommitmentFailure(saveError()!)}</p> : null}
                {saveNotice() ? <p role="status">{saveNotice()}</p> : null}
                <Button variant="primary" type="submit" disabled={saving()}>
                  {saving() ? 'Saving…' : 'Save draft'}
                </Button>
              </Stack>
            </form>
          </CardContent>
        </Card>
        {pendingDecision ? (
          <ResponsibilitiesPanel
            programId={programId}
            scope={{ recordType: 'commitment', recordId: draftId, versionId: draftId, revision: current.revision }}
            members={members.value ?? []}
            assignments={assignments}
            onChanged={(revision) => setAssignmentsWritten(revision)}
            waiverPath={exceptionsPath}
          />
        ) : null}
        {decisions.pending && !decisions.value ? (
          <Spinner label="Loading decisions" />
        ) : (
          <CommitmentDecisionPanel
            programId={programId}
            draft={current}
            decisions={decisionList}
            authors={pendingAuthors(revisionList, versionList)}
            conflicts={standingConflicts(assignments.value?.assignments ?? [])}
            exceptionsPath={exceptionsPath}
            onDecided={() => refresh(current.revision)}
          />
        )}
        <Card>
          <CardHeader>
            <CardTitle>Effective version</CardTitle>
            <CardDescription>The approved commitment in force on a given date.</CardDescription>
          </CardHeader>
          <CardContent>
            <Stack gap="sm">
              <label className="registration-field">
                <span>Effective on</span>
                <input type="date" value={effectiveOn()} onInput={(event: Event) => setEffectiveOn(inputValue(event))} />
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
                    Version {effective.value.version} (revision {effective.value.revision}), effective {effective.value.effectiveFrom}.
                  </p>
                  <VersionSummary version={effective.value} />
                </Stack>
              ) : (
                <p>No version is effective on this date.</p>
              )}
            </Stack>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Effective versions</CardTitle>
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
            ) : versionList.length === 0 ? (
              <p>No effective versions yet.</p>
            ) : (
              <ol className="plain-list commitment-versions">
                {versionList.map((item) => (
                  <li>
                    Version {item.version} · revision {item.revision} · effective {item.effectiveFrom} · source{' '}
                    {label(item.sourceResolution)}
                  </li>
                ))}
              </ol>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Revision history</CardTitle>
          </CardHeader>
          <CardContent>
            {revisions.pending && !revisions.value ? (
              <Spinner label="Loading history" />
            ) : revisions.error ? (
              <Stack gap="sm">
                <p role="alert">{revisions.error.message}</p>
                <Button variant="secondary" onPress={() => revisions.refresh()}>
                  Try again
                </Button>
              </Stack>
            ) : (
              <Stack gap="sm">
                <ol className="plain-list commitment-revisions">
                  {revisionList.map((item) => (
                    <li>
                      <Button
                        variant="secondary"
                        aria-pressed={item.revision === selectedRevision() ? 'true' : 'false'}
                        onPress={() => setSelectedRevision(item.revision === selectedRevision() ? null : item.revision)}
                      >
                        Revision {item.revision}
                      </Button>{' '}
                      <span className="commitment-meta">
                        by {item.changedBy} on {new Date(item.changedAt).toLocaleDateString()}
                      </span>
                    </li>
                  ))}
                </ol>
                {selected ? (
                  <section aria-label={`Revision ${selected.revision}`}>
                    <h3>Revision {selected.revision}</h3>
                    <p>{selected.statement}</p>
                    {selected.context ? <p>{selected.context}</p> : null}
                    <p>Source: {selected.sourceReference}</p>
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
              <ol className="plain-list commitment-decisions">
                {decisionList.map((decision) => (
                  <li>
                    <strong>
                      {decision.stage === 'approval' ? 'Approved' : decision.outcome === 'accept' ? 'Review accepted' : 'Changes requested'}
                    </strong>{' '}
                    revision {decision.revision} by {decision.actor} on {new Date(decision.decidedAt).toLocaleString()}:{' '}
                    {decision.rationale}
                    {decision.sourceVerification ? ` · source ${label(decision.sourceVerification)}` : ''}
                    {decision.effectiveFrom ? ` · effective ${decision.effectiveFrom}` : ''}
                    {decision.waiverId ? (
                      <>
                        {' '}
                        · under <a href={`${exceptionsPath}?exception=${decision.waiverId}`}>exception {shortId(decision.waiverId)}</a>
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
