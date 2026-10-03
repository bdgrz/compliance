import { state } from '@askrjs/askr';
import { resource } from '@askrjs/askr/resources';
import { Button, Spinner, Stack } from '@askrjs/themes/components';

import { getCurrentControlVersion } from '../controls/lifecycle.js';
import { Feedback, useAction } from './actions.js';
import {
  listControlMappings,
  ProgramRequestError,
  proposeControlMapping,
  proposeCriterionNotApplicable,
  reviewCriterionApplicability,
  withdrawCriterionNotApplicable,
  type ApplicabilityDecision,
  type CoverageRow,
} from './mappings.js';

type MappedControl = CoverageRow['mappedControls'][number];

function textValue(event: Event) {
  return (event.target as HTMLInputElement | HTMLTextAreaElement).value;
}

export function ProposeNotApplicableForm({
  programId,
  editionId,
  criterion,
  existing,
  onChanged,
}: {
  programId: string;
  editionId: string;
  criterion: string;
  existing: ApplicabilityDecision | null;
  onChanged: () => void;
}) {
  const [rationale, setRationale] = state('');
  const action = useAction();

  function submit(event: Event) {
    event.preventDefault();
    void action.run(async () => {
      await proposeCriterionNotApplicable(
        programId,
        editionId,
        criterion,
        existing?.revision ?? 0,
        rationale()
      );
      setRationale('');
      onChanged();
      return `Proposed ${criterion} as not applicable. It counts only after review.`;
    });
  }

  return (
    <details className="criteria-action">
      <summary>Mark {criterion} not applicable</summary>
      <form onSubmit={submit} aria-label={`Mark ${criterion} not applicable`}>
        <Stack gap="sm">
          <label className="registration-field">
            <span>Why this criterion does not apply</span>
            <textarea
              rows={3}
              maxLength={4000}
              value={rationale()}
              onInput={(event: Event) => setRationale(textValue(event))}
              required
            />
          </label>
          <Feedback failure={action.failure()} notice={action.notice()} />
          <Button variant="secondary" type="submit" disabled={action.pending()}>
            {action.pending() ? 'Proposing…' : 'Propose not applicable'}
          </Button>
        </Stack>
      </form>
    </details>
  );
}

export function ReviewNotApplicableForm({
  programId,
  decision,
  onChanged,
}: {
  programId: string;
  decision: ApplicabilityDecision;
  onChanged: () => void;
}) {
  const [outcome, setOutcome] = state<'accept' | 'reject'>('accept');
  const [rationale, setRationale] = state('');
  const [waiverId, setWaiverId] = state('');
  const action = useAction();
  const criterion = decision.criterionIdentifier;

  function submit(event: Event) {
    event.preventDefault();
    void action.run(async () => {
      await reviewCriterionApplicability(
        programId,
        decision.decisionId,
        decision.revision,
        outcome(),
        rationale(),
        waiverId()
      );
      setRationale('');
      onChanged();
      return outcome() === 'accept'
        ? `Accepted ${criterion} as not applicable.`
        : `Rejected the not-applicable proposal for ${criterion}.`;
    });
  }

  return (
    <form
      onSubmit={submit}
      aria-label={`Review not-applicable proposal for ${criterion}`}
    >
      <Stack gap="sm">
        <fieldset className="control-outcome">
          <legend>Review the not-applicable proposal</legend>
          <label>
            <input
              type="radio"
              name={`applicability-outcome-${decision.decisionId}`}
              checked={outcome() === 'accept'}
              onChange={() => setOutcome('accept')}
            />{' '}
            Accept
          </label>
          <label>
            <input
              type="radio"
              name={`applicability-outcome-${decision.decisionId}`}
              checked={outcome() === 'reject'}
              onChange={() => setOutcome('reject')}
            />{' '}
            Reject
          </label>
        </fieldset>
        <label className="registration-field">
          <span>Review rationale</span>
          <textarea
            rows={2}
            maxLength={4000}
            value={rationale()}
            onInput={(event: Event) => setRationale(textValue(event))}
            required
          />
        </label>
        <label className="registration-field">
          <span>Separation-of-duties waiver ID (optional)</span>
          <input
            type="text"
            value={waiverId()}
            onInput={(event: Event) => setWaiverId(textValue(event))}
          />
        </label>
        <Feedback failure={action.failure()} notice={action.notice()} />
        <Button variant="primary" type="submit" disabled={action.pending()}>
          {action.pending() ? 'Recording…' : 'Record review'}
        </Button>
      </Stack>
    </form>
  );
}

export function WithdrawNotApplicableForm({
  programId,
  decision,
  onChanged,
}: {
  programId: string;
  decision: ApplicabilityDecision;
  onChanged: () => void;
}) {
  const [rationale, setRationale] = state('');
  const action = useAction();
  const criterion = decision.criterionIdentifier;

  function submit(event: Event) {
    event.preventDefault();
    void action.run(async () => {
      await withdrawCriterionNotApplicable(
        programId,
        decision.decisionId,
        decision.revision,
        rationale()
      );
      setRationale('');
      onChanged();
      return `Withdrew the not-applicable decision. ${criterion} returns to coverage.`;
    });
  }

  return (
    <details className="criteria-action">
      <summary>Withdraw not applicable for {criterion}</summary>
      <form
        onSubmit={submit}
        aria-label={`Withdraw not-applicable decision for ${criterion}`}
      >
        <Stack gap="sm">
          <label className="registration-field">
            <span>Withdrawal rationale</span>
            <textarea
              rows={2}
              maxLength={4000}
              value={rationale()}
              onInput={(event: Event) => setRationale(textValue(event))}
              required
            />
          </label>
          <Feedback failure={action.failure()} notice={action.notice()} />
          <Button variant="secondary" type="submit" disabled={action.pending()}>
            {action.pending() ? 'Withdrawing…' : 'Withdraw decision'}
          </Button>
        </Stack>
      </form>
    </details>
  );
}

async function loadRemapContext(
  programId: string,
  editionId: string,
  controlId: string
) {
  const [current, mappings] = await Promise.all([
    getCurrentControlVersion(programId, controlId),
    listControlMappings(programId, controlId, editionId),
  ]);
  return { current, mappings };
}

// A mapping that cites a superseded control version is never moved silently; this proposes a new
// mapping to the control's current approved version, which counts only after independent review.
export function RemapForm({
  programId,
  editionId,
  criterion,
  control,
  onChanged,
}: {
  programId: string;
  editionId: string;
  criterion: string;
  control: MappedControl;
  onChanged: () => void;
}) {
  const context = resource(
    () => loadRemapContext(programId, editionId, control.controlId),
    [programId, editionId, control.controlId]
  );
  const [rationale, setRationale] = state<string | null>(null);
  const [explanation, setExplanation] = state<string | null>(null);
  const action = useAction();

  if (context.pending && !context.value)
    return <Spinner label={`Loading remap details for ${criterion}`} />;
  if (context.error) {
    return context.error instanceof ProgramRequestError &&
      context.error.status === 403 ? (
      <p>You do not have permission to remap this mapping.</p>
    ) : (
      <Stack gap="sm">
        <p role="alert">{context.error.message}</p>
        <Button variant="secondary" onPress={() => context.refresh()}>
          Try again
        </Button>
      </Stack>
    );
  }

  const current = context.value?.current ?? null;
  const mapping = context.value?.mappings.find(
    (candidate) =>
      candidate.mappingId === control.mappingId ||
      candidate.criterionIdentifier === criterion
  );
  if (!current) {
    return (
      <p className="control-meta">
        This control has no current approved version, so there is nothing to
        remap to. Approve a version of the control first.
      </p>
    );
  }
  if (!mapping) {
    return (
      <p className="control-meta">
        The mapping could not be found. Reload the page to see its latest state.
      </p>
    );
  }
  const reviewed = mapping.versions.find(
    (version) => version.versionNumber === mapping.activeVersionNumber
  );
  const rationaleValue = rationale() ?? reviewed?.rationale ?? '';
  const explanationValue =
    explanation() ??
    reviewed?.applicabilityExplanation ??
    control.applicabilityExplanation;

  function submit(event: Event) {
    event.preventDefault();
    if (!current || !mapping) return;
    void action.run(async () => {
      await proposeControlMapping(programId, {
        controlId: control.controlId,
        controlVersionId: current.versionId,
        editionId,
        criterionIdentifier: criterion,
        expectedRevision: mapping.revision,
        rationale: rationaleValue,
        applicabilityExplanation: explanationValue,
      });
      onChanged();
      return `Proposed a mapping to the current control version. It counts toward coverage only after review.`;
    });
  }

  return (
    <form
      onSubmit={submit}
      aria-label={`Remap ${criterion} to the current control version`}
    >
      <Stack gap="sm">
        <label className="registration-field">
          <span>Why this control addresses it</span>
          <textarea
            rows={3}
            maxLength={4000}
            value={rationaleValue}
            onInput={(event: Event) => setRationale(textValue(event))}
            required
          />
        </label>
        <label className="registration-field">
          <span>Applicability explanation</span>
          <textarea
            rows={3}
            maxLength={4000}
            value={explanationValue}
            onInput={(event: Event) => setExplanation(textValue(event))}
            required
          />
        </label>
        <Feedback failure={action.failure()} notice={action.notice()} />
        <Button variant="primary" type="submit" disabled={action.pending()}>
          {action.pending() ? 'Proposing…' : 'Propose remapped mapping'}
        </Button>
      </Stack>
    </form>
  );
}
