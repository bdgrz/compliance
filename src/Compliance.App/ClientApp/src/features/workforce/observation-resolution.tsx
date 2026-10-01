import { state } from '@askrjs/askr';
import { Button, Stack } from '@askrjs/themes/components';

import { resolveWorkforceObservation, type ObservationResolution } from './workforce.js';
import { inputValue } from './workforce-shared.js';

// Who closed an observation, how, and why.
export function ResolutionSummary({ resolution }: { resolution: ObservationResolution }) {
  return (
    <p className="workforce-resolution">
      {resolution.resolution === 'dismissed' ? 'Dismissed' : 'Resolved'} by {resolution.resolvedBy} on{' '}
      {new Date(resolution.resolvedAt).toLocaleString()}: {resolution.note}
    </p>
  );
}

// Closes one open observation with a required note. The server decides; the list reloads after.
export function ResolveObservationForm({
  observationId,
  label,
  onClosed,
}: {
  observationId: string;
  label: string;
  onClosed: (message: string) => void;
}) {
  const [outcome, setOutcome] = state<'resolved' | 'dismissed'>('resolved');
  const [note, setNote] = state('');
  const [pending, setPending] = state(false);
  const [error, setError] = state<string | null>(null);

  async function close(event: Event) {
    event.preventDefault();
    setError(null);
    if (note().trim().length === 0) {
      setError('Explain the resolution in a note.');
      return;
    }
    setPending(true);
    try {
      await resolveWorkforceObservation(observationId, outcome(), note());
      onClosed(`${outcome() === 'dismissed' ? 'Dismissed' : 'Resolved'}: ${label}.`);
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Unable to close this observation.');
      setPending(false);
    }
  }

  return (
    <form className="workforce-resolve" aria-label={`Close ${label}`} onSubmit={(event: Event) => void close(event)}>
      <Stack gap="sm">
        <label className="registration-field">
          <span>Outcome</span>
          <select
            value={outcome()}
            onChange={(event: Event) => setOutcome(inputValue(event) === 'dismissed' ? 'dismissed' : 'resolved')}
          >
            <option value="resolved">Resolved</option>
            <option value="dismissed">Dismissed (not a real issue)</option>
          </select>
        </label>
        <label className="registration-field">
          <span>Note</span>
          <textarea value={note()} maxLength={1000} onInput={(event: Event) => setNote(inputValue(event))} required />
        </label>
        {error() ? <p role="alert">{error()}</p> : null}
        <Button variant="secondary" size="sm" type="submit" disabled={pending()}>
          {pending() ? 'Closing…' : 'Close observation'}
        </Button>
      </Stack>
    </form>
  );
}
