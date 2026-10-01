import { type SourceDecision } from './source-observations.js';

export function SourceProvenance({
  decision,
  currentRevision,
  acceptedForCurrent,
}: {
  decision: SourceDecision;
  currentRevision?: number | null;
  acceptedForCurrent?: boolean | null;
}) {
  const accepted = decision.outcome === 'accepted';
  const summary = !accepted
    ? `Dismissed; canonical revision ${decision.target_revision} retained.`
    : currentRevision == null
      ? `Accepted for revision ${decision.target_revision}`
      : acceptedForCurrent === true
        ? `Accepted for the current revision ${decision.target_revision}`
        : `Accepted for historical revision ${decision.target_revision}; current revision is ${currentRevision}.`;
  return (
    <div role="status">
      <p>{summary}</p>
      <p>
        {decision.note} — {decision.actor.display},{' '}
        {new Date(decision.decided_at).toLocaleString()}
      </p>
      <p>
        This attributed decision is immutable. Record a new source revision for
        new evidence.
      </p>
    </div>
  );
}
