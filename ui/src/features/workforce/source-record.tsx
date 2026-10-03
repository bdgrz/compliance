import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import {
  Button,
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  Spinner,
  Stack,
} from '@askrjs/themes/components';

import { organizationPath } from '../tenants/tenants.js';
import {
  listSourceObservations,
  recordSourceObservation,
  sourceLabel,
  type SourceFacts,
  type SourceIdentity,
  type SourceTarget,
} from './source-observations.js';
import { inputValue, LoadFailure } from './workforce-shared.js';
import { SourceFactsFields } from './source-facts.js';
import { isStaleConflict } from './workforce.js';

function localDateTime() {
  const now = new Date();
  return new Date(now.getTime() - now.getTimezoneOffset() * 60_000)
    .toISOString()
    .slice(0, 16);
}

function RecordSourceForm({
  target,
  onRecorded,
}: {
  target: SourceTarget;
  onRecorded: (id: string) => void;
}) {
  const [source, setSource] = state<SourceIdentity>({
    source_kind: target.kind === 'service_identity' ? 'provider' : 'hris',
    source_system: '',
    source_record_id: '',
    source_revision: '',
  });
  const [facts, setFacts] = state<SourceFacts>(target.facts);
  const [observedAt, setObservedAt] = state(localDateTime());
  const [pending, setPending] = state(false);
  const [error, setError] = state<Error | null>(null);

  async function record(event: Event) {
    event.preventDefault();
    if (pending()) return;
    setPending(true);
    setError(null);
    try {
      const id = await recordSourceObservation(
        target,
        source(),
        facts(),
        new Date(observedAt()).toISOString()
      );
      onRecorded(id);
    } catch (failure) {
      setError(
        failure instanceof Error
          ? failure
          : new Error('Unable to record this source observation.')
      );
    } finally {
      setPending(false);
    }
  }

  return (
    <form
      className="workforce-source-record"
      onSubmit={(event: Event) => void record(event)}
    >
      <Stack gap="sm">
        <p>
          Explicitly correlated to this canonical record at revision{' '}
          {target.revision}. Enter the source facts as observed. A source
          revision is immutable; new evidence needs a new source revision.
        </p>
        <label className="registration-field">
          <span>Source kind</span>
          <select
            value={source().source_kind}
            onChange={(event: Event) =>
              setSource({
                ...source(),
                source_kind: inputValue(event) as SourceIdentity['source_kind'],
              })
            }
          >
            {target.kind === 'service_identity' ? (
              <option value="provider">Provider (corroborating)</option>
            ) : (
              <>
                <option value="hris">HRIS (authoritative)</option>
                <option value="idp">Identity provider (corroborating)</option>
              </>
            )}
          </select>
        </label>
        {(
          ['source_system', 'source_record_id', 'source_revision'] as const
        ).map((key) => (
          <label className="registration-field">
            <span>
              {
                {
                  source_system: 'Source system',
                  source_record_id: 'Source record ID',
                  source_revision: 'Source revision',
                }[key]
              }
            </span>
            <input
              value={source()[key]}
              required
              maxLength={key === 'source_record_id' ? 500 : 200}
              onInput={(event: Event) =>
                setSource({ ...source(), [key]: inputValue(event) })
              }
            />
          </label>
        ))}
        <label className="registration-field">
          <span>Observed at</span>
          <input
            type="datetime-local"
            value={observedAt()}
            required
            onInput={(event: Event) => setObservedAt(inputValue(event))}
          />
        </label>
        <SourceFactsFields
          kind={target.kind}
          facts={facts()}
          onChange={setFacts}
        />
        {target.kind === 'work_relationship' ? (
          <p>
            Recording and comparing relationship facts requires both
            manager-chain and personal-details read grants, even when values are
            empty.
          </p>
        ) : null}
        {target.kind === 'service_identity' ? (
          <p>
            Provider evidence corroborates identity facts and credential expiry.
            Owner, purpose, and ownership review remain governed by this
            organization.
          </p>
        ) : null}
        {error() ? <p role="alert">{error()!.message}</p> : null}
        {isStaleConflict(error()) ? (
          <p>
            Keep the original content for a retry. New evidence needs a new
            source revision. If the canonical revision changed, reload the
            canonical record before recording.
          </p>
        ) : null}
        <Button variant="primary" type="submit" disabled={pending()}>
          {pending() ? 'Recording…' : 'Record source observation'}
        </Button>
      </Stack>
    </form>
  );
}

export function SourceObservationsPanel({
  target,
  restrictedFieldsRedacted,
}: {
  target: SourceTarget;
  restrictedFieldsRedacted?: boolean;
}) {
  const [version, setVersion] = state(0);
  const [recordedId, setRecordedId] = state<string | null>(null);
  const observations = resource(
    () => listSourceObservations(target),
    [target.kind, target.id, version()]
  );
  return (
    <Card>
      <CardHeader>
        <CardTitle>Source observations</CardTitle>
      </CardHeader>
      <CardContent>
        <Stack gap="sm">
          <a href={organizationPath('/workforce/sources')}>
            All workforce sources
          </a>
          {observations.pending ? (
            <Spinner label="Loading source observations" />
          ) : observations.error ? (
            <LoadFailure
              error={observations.error}
              onRetry={() => observations.refresh()}
            />
          ) : observations.value?.length ? (
            <ul>
              {observations.value.map((item) => (
                <li>
                  <a
                    href={organizationPath(
                      `/workforce/sources/${item.observation_id}`
                    )}
                  >
                    {sourceLabel(item.source.source_kind)} ·{' '}
                    {item.source.source_system} · {item.source.source_record_id}{' '}
                    · {item.source.source_revision}
                  </a>
                  {item.decision
                    ? ` · ${item.decision.outcome} for revision ${item.decision.target_revision}`
                    : ' · Needs review'}
                </li>
              ))}
            </ul>
          ) : (
            <p>No source observations for this record.</p>
          )}
          {recordedId() ? (
            <p role="status">
              Source observation recorded.{' '}
              <a href={organizationPath(`/workforce/sources/${recordedId()}`)}>
                Review source facts
              </a>
            </p>
          ) : null}
          {restrictedFieldsRedacted ? (
            <p>
              Source recording needs unrestricted relationship details. No
              hidden values are assumed to be empty.
            </p>
          ) : (
            <details>
              <summary>Record source observation</summary>
              <div key={`${target.kind}:${target.id}`}>
                <RecordSourceForm
                  target={target}
                  onRecorded={(id) => {
                    setRecordedId(id);
                    setVersion(version() + 1);
                  }}
                />
              </div>
            </details>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
}
