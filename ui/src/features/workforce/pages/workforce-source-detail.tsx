import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
  Button,
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  Page,
  PageHeader,
  Spinner,
  Stack,
} from '@askrjs/themes/components';

import { organizationPath } from '../../tenants/tenants.js';
import {
  decideSourceObservation,
  getSourceObservation,
  previewSourceObservation,
  sourceLabel,
  sourceTargetPath,
  type SourceObservation,
} from '../source-observations.js';
import { inputValue, LoadFailure, RecordFailure } from '../workforce-shared.js';
import { SourceFactsSummary } from '../source-facts.js';
import { SourceProvenance } from '../source-provenance.js';
import { isStaleConflict } from '../workforce.js';

function SourceReview({ observation }: { observation: SourceObservation }) {
  const [note, setNote] = state('');
  const [pending, setPending] = state(false);
  const [error, setError] = state<Error | null>(null);
  const [needsRefresh, setNeedsRefresh] = state(false);
  const preview = resource(
    () => previewSourceObservation(observation.observation_id),
    [observation.observation_id]
  );
  if (preview.pending) return <Spinner label="Comparing source facts" />;
  if (preview.error)
    return (
      <Stack gap="sm">
        {observation.decision ? (
          <SourceProvenance
            decision={observation.decision}
            currentRevision={observation.current_target_revision}
            acceptedForCurrent={observation.accepted_for_current_revision}
          />
        ) : null}
        <LoadFailure error={preview.error} onRetry={() => preview.refresh()} />
      </Stack>
    );
  const current = preview.value!;
  async function decide(outcome: 'accepted' | 'dismissed') {
    if (
      pending() ||
      needsRefresh() ||
      !note().trim() ||
      current.decision ||
      (outcome === 'accepted' && !current.can_accept)
    )
      return;
    setPending(true);
    setError(null);
    try {
      await decideSourceObservation(current, outcome, note());
      preview.refresh();
    } catch (failure) {
      setError(
        failure instanceof Error
          ? failure
          : new Error('Unable to decide this source observation.')
      );
      if (isStaleConflict(failure)) setNeedsRefresh(true);
    } finally {
      setPending(false);
    }
  }
  return (
    <Stack gap="sm">
      <p>
        {current.source_authority.replaceAll('_', ' ')} source · Canonical
        revision {current.current_target_revision}
      </p>
      {current.conflicting_fields.length ||
      current.restricted_fields_conflict ? (
        <p role="note">
          Conflicting fields:{' '}
          {current.conflicting_fields
            .map((field) => field.replaceAll('_', ' '))
            .join(', ')}
          {current.restricted_fields_conflict
            ? ' · Restricted workforce facts differ.'
            : null}
        </p>
      ) : (
        <p>Observed facts match the canonical record.</p>
      )}
      <a
        href={organizationPath(
          sourceTargetPath(observation.target_kind, observation.target_id)
        )}
      >
        Correct canonical record
      </a>
      <p>
        Correct the canonical record explicitly, then refresh this preview
        before accepting. A decision never changes canonical facts or access.
      </p>
      <Button
        type="button"
        variant="secondary"
        disabled={pending()}
        onPress={() => {
          setNeedsRefresh(false);
          setError(null);
          preview.refresh();
        }}
      >
        Refresh comparison
      </Button>
      {current.decision ? (
        <SourceProvenance
          decision={current.decision}
          currentRevision={current.current_target_revision}
          acceptedForCurrent={current.accepted_for_current_revision}
        />
      ) : (
        <>
          <label className="registration-field">
            <span>Decision note (required)</span>
            <textarea
              value={note()}
              maxLength={1000}
              required
              onInput={(event: Event) => setNote(inputValue(event))}
            />
          </label>
          {error() ? <p role="alert">{error()!.message}</p> : null}
          {needsRefresh() ? (
            <p role="alert">
              Refresh comparison before deciding again. The source or canonical
              revision changed.
            </p>
          ) : null}
          <Button
            type="button"
            variant="primary"
            disabled={
              pending() ||
              needsRefresh() ||
              !note().trim() ||
              !current.can_accept
            }
            onPress={() => void decide('accepted')}
          >
            Accept matching source
          </Button>
          <Button
            type="button"
            variant="secondary"
            disabled={pending() || needsRefresh() || !note().trim()}
            onPress={() => void decide('dismissed')}
          >
            Dismiss and keep canonical
          </Button>
        </>
      )}
    </Stack>
  );
}

export function WorkforceSourceDetailPage({
  observationId,
}: {
  observationId: string;
}) {
  const observation = resource(
    () => getSourceObservation(observationId),
    [observationId]
  );
  if (observation.pending)
    return (
      <Page>
        <Spinner label="Loading source observation" />
      </Page>
    );
  if (observation.error)
    return (
      <RecordFailure
        error={observation.error}
        noun="source observation"
        backPath="/workforce/sources"
        backLabel="Back to workforce sources"
        onRetry={() => observation.refresh()}
      />
    );
  const current = observation.value!;
  return (
    <Page>
      <PageHeader
        title={`${current.source.source_system} · ${current.source.source_record_id}`}
        description={`${sourceLabel(current.source.source_kind)} · Source revision ${current.source.source_revision}`}
      />
      <Stack gap="md">
        <a href={organizationPath('/workforce/sources')}>
          Back to workforce sources
        </a>
        <Card>
          <CardHeader>
            <CardTitle>Immutable source observation</CardTitle>
          </CardHeader>
          <CardContent>
            <p>
              Observed {new Date(current.observed_at).toLocaleString()} against
              canonical revision {current.observed_target_revision}.
            </p>
            <p>
              <a
                href={organizationPath(
                  sourceTargetPath(current.target_kind, current.target_id)
                )}
              >
                Canonical {current.target_kind.replaceAll('_', ' ')}
              </a>
            </p>
            <p>
              Recorded by {current.recorded_by.display} on{' '}
              {new Date(current.recorded_at).toLocaleString()}.
            </p>
            <SourceFactsSummary
              kind={current.target_kind}
              facts={current.facts}
              restrictedFieldsRedacted={current.restricted_fields_redacted}
            />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Review source facts</CardTitle>
          </CardHeader>
          <CardContent>
            <SourceReview observation={current} />
          </CardContent>
        </Card>
      </Stack>
    </Page>
  );
}
