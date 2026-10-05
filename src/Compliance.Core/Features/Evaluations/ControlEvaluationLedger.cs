using System.Globalization;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     One program's control design and implementation evaluations in one stream (M0-D13). Each
///     evaluation freezes its procedure and exact control version when it starts, records separate
///     design, implementation, and evidence-sufficiency conclusions per round, and keeps every
///     submission and independent review. Deviations are never deleted: material ones route to a
///     finding and require a retest, minor ones need a disposition. Concurrency is per evaluation.
/// </summary>
public sealed class ControlEvaluationLedger : Aggregate
{
    public const string InProgress = "in_progress";
    public const string Submitted = "submitted";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
    public const string ChangesRequested = "changes_requested";
    public const string Met = "met";
    public const string NotMet = "not_met";
    public const string NotTested = "not_tested";
    public const string Effective = "effective";
    public const string EffectiveWithExceptions = "effective_with_exceptions";
    public const string Ineffective = "ineffective";
    public const string Minor = "minor";
    public const string Material = "material";
    public const string Corrected = "corrected";
    public const string AcceptedWithWaiver = "accepted_with_waiver";
    public const string PendingDisposition = "pending_disposition";
    public const string Dispositioned = "dispositioned";
    public const string PendingRouting = "pending_routing";
    public const string RoutedToFinding = "routed_to_finding";
    public const string NotRequired = "not_required";
    public const string Required = "required";
    public const string Passed = "passed";
    public const string Failed = "failed";

    public static readonly IReadOnlyList<string> Assertions =
        ["design", "implementation", "evidence_sufficiency"];

    static readonly string[] Methods = ["inquiry", "inspection", "observation", "reperformance"];
    const int MaximumTextLength = 4000;
    const int MaximumSteps = 100;
    const int MaximumItems = 50;

    readonly Uuid _tenantId;
    readonly Dictionary<Uuid, EvaluationState> _evaluations = [];

    public ControlEvaluationLedger(Uuid tenantId, Uuid programId)
        : base(programId, new EventStreamAddress(tenantId.ToString(), "control-evaluations",
            programId.ToString()))
    {
        _tenantId = tenantId;
        On<ControlEvaluationStarted>(ev => _evaluations[ev.EvaluationId] = new EvaluationState(ev));
        On<ControlEvaluationStepRecorded>(ev =>
        {
            var evaluation = _evaluations[ev.EvaluationId];
            evaluation.Revision = ev.Revision;
            evaluation.Results[ev.StepId] = ev.Result;
            evaluation.Deviations.RemoveAll(deviation => deviation.StepId == ev.StepId &&
                deviation.Round == evaluation.Round);
            if (ev.Deviation is not null)
                evaluation.Deviations.Add(ev.Deviation);
        });
        On<ControlEvaluationDeviationDisposed>(ev =>
        {
            var evaluation = _evaluations[ev.EvaluationId];
            evaluation.Revision = ev.Revision;
            var index = evaluation.Deviations.FindIndex(d => d.DeviationId == ev.DeviationId);
            evaluation.Deviations[index] = evaluation.Deviations[index] with
            {
                Status = Dispositioned,
                Disposition = ev.Disposition,
                DispositionRationale = ev.Rationale,
                WaiverId = ev.WaiverId,
            };
        });
        On<ControlEvaluationSubmitted>(ev =>
        {
            var evaluation = _evaluations[ev.EvaluationId];
            evaluation.Revision = ev.Revision;
            evaluation.State = Submitted;
            evaluation.Submissions.Add(ev.Submission);
        });
        On<ControlEvaluationReviewed>(ev =>
        {
            var evaluation = _evaluations[ev.EvaluationId];
            evaluation.Revision = ev.Revision;
            evaluation.Reviews.Add(ev.Review);
            switch (ev.Review.Decision)
            {
                case Accepted:
                    evaluation.State = Accepted;
                    break;
                case Rejected:
                    evaluation.State = InProgress;
                    evaluation.Round++;
                    evaluation.Results.Clear();
                    break;
                default:
                    evaluation.State = InProgress;
                    evaluation.Round++;
                    break;
            }
        });
    }

    public bool Contains(Uuid evaluationId) => _evaluations.ContainsKey(evaluationId);

    public CommandFailure? Start(Uuid controlId, Uuid evaluationId, Uuid controlVersionId,
        Uuid planVersionId, long planVersion, IReadOnlyList<EvaluationProcedureStep>? steps,
        Uuid? retestOfEvaluationId,
        Uuid evaluatorMemberId, string evaluatorDisplay, DateTimeOffset startedAt)
    {
        if (_evaluations.TryGetValue(evaluationId, out var existing))
            return existing.Started.ControlId == controlId &&
                   existing.Started.ControlVersionId == controlVersionId &&
                   existing.Started.PlanVersionId == planVersionId &&
                   existing.Started.PlanVersion == planVersion &&
                   existing.Started.RetestOfEvaluationId == retestOfEvaluationId &&
                   existing.Started.EvaluatorMemberId == evaluatorMemberId &&
                   SameProcedure(existing.Started.Steps, steps)
                ? null
                : CommandFailure.StateConflict("The evaluation request was already recorded.");
        if (planVersionId == Uuid.Empty || planVersion < 1)
            return CommandFailure.InvalidContent(
                "An evaluation must use an exact control-evaluation plan version.");
        IReadOnlyList<Uuid> retestDeviations = [];
        if (retestOfEvaluationId is { } originalId)
        {
            if (!_evaluations.TryGetValue(originalId, out var original) ||
                original.Started.ControlId != controlId)
                return CommandFailure.MissingRecord("The evaluation to retest was not found.");
            var accepted = AcceptedSubmission(original);
            if (accepted is null || !accepted.Deviations.Any(static d => d.Classification == Material))
                return CommandFailure.StateConflict(
                    "Only an accepted evaluation with a material deviation can be retested.");
            if (original.Started.PlanVersion is { } originalPlanVersion &&
                planVersion < originalPlanVersion)
                return CommandFailure.StateConflict(
                    "A retest must use its original plan version or a successor version.");
            retestDeviations = accepted.Deviations.Where(static d => d.Classification == Material)
                .Select(static d => d.DeviationId).ToArray();
        }
        if (ValidateProcedure(steps) is { } invalid)
            return invalid;
        RaiseEvent(new ControlEvaluationStarted(_tenantId, Id, controlId, evaluationId, 1,
            controlVersionId, steps!.Select(Clean).ToArray(), evaluatorMemberId,
            ActorReference.ForMember(evaluatorMemberId, evaluatorDisplay), startedAt,
            retestOfEvaluationId, retestDeviations)
        {
            PlanVersionId = planVersionId,
            PlanVersion = planVersion,
        });
        return null;
    }

    public static CommandFailure? ValidateProcedure(IReadOnlyList<EvaluationProcedureStep>? steps)
    {
        if (steps is null || steps.Count is < 1 or > MaximumSteps || steps.Any(static step =>
                step is null || !Assertions.Contains(step.Assertion) ||
                !Methods.Contains(step.Method) || !IsBoundedText(step.ExpectedCondition) ||
                step.InspectedItems is null || step.InspectedItems.Count is < 1 or > MaximumItems))
            return CommandFailure.InvalidContent(
                "A procedure needs 1 to 100 steps, each with an assertion of design, implementation, or evidence_sufficiency, a method of inquiry, inspection, observation, or reperformance, an expected condition, and 1 to 50 inspected items.");
        if (steps.Any(static step => !AreItems(step.InspectedItems, required: true)))
            return CommandFailure.InvalidContent(
                "Each inspected item needs a kind of record, artifact, boundary, commitment, risk, criterion, control, policy, provider, or evidence, plus an exact reference and version.");
        return null;
    }

    static bool SameProcedure(IReadOnlyList<EvaluationProcedureStep> existing,
        IReadOnlyList<EvaluationProcedureStep>? candidate) => candidate is not null &&
        existing.Count == candidate.Count && existing.Zip(candidate).All(static pair =>
            pair.First.Assertion == pair.Second.Assertion &&
            pair.First.Method == pair.Second.Method &&
            pair.First.ExpectedCondition == pair.Second.ExpectedCondition &&
            pair.First.InspectedItems.SequenceEqual(pair.Second.InspectedItems));

    public CommandFailure? RecordStep(Uuid controlId, Uuid evaluationId, Uuid stepId,
        long expectedRevision, string result, string rationale,
        IReadOnlyList<EvaluationInspectedItem>? inspectedItems, string? classification,
        string? description, Uuid actorMemberId, string actorDisplay, DateTimeOffset recordedAt)
    {
        if (Find(controlId, evaluationId) is not { } evaluation)
            return CommandFailure.MissingRecord("The control evaluation was not found.");
        if (Writable(evaluation, expectedRevision, actorMemberId) is { } blocked)
            return blocked;
        if (StepIndex(evaluation, stepId) < 0)
            return CommandFailure.MissingRecord("The procedure step was not found.");
        if (result is not (Met or NotMet or NotTested) || !IsBoundedText(rationale) ||
            !AreItems(inspectedItems, required: result != NotTested))
            return CommandFailure.InvalidContent(
                "A step result must be met, not_met, or not_tested, with a rationale and, unless not tested, the exact items inspected.");
        if (result == NotMet
                ? classification is not (Minor or Material) || !IsBoundedText(description)
                : classification is not null || description is not null)
            return CommandFailure.InvalidContent(
                "A not_met step requires a deviation classified minor or material with a description; other results cannot carry one.");
        var actor = ActorReference.ForMember(actorMemberId, actorDisplay);
        EvaluationDeviationView? deviation = null;
        if (result == NotMet)
        {
            // Stable per evaluation step, so a resubmitted deviation reuses its finding.
            var deviationId = Uuid.CreateVersion5(evaluationId, "deviation-" + stepId);
            deviation = new EvaluationDeviationView(deviationId, stepId, evaluation.Round,
                classification!, description!.Trim(),
                classification == Material ? PendingRouting : PendingDisposition,
                classification == Material ? Uuid.CreateVersion5(deviationId, "finding") : null,
                null, null, null, actor, recordedAt);
        }
        RaiseEvent(new ControlEvaluationStepRecorded(_tenantId, Id, controlId, evaluationId,
            evaluation.Revision + 1, stepId, new EvaluationStepResultView(evaluation.Round, result,
                rationale.Trim(), (inspectedItems ?? []).Select(Clean).ToArray(), actor,
                recordedAt), deviation));
        return null;
    }

    public CommandFailure? DisposeDeviation(Uuid controlId, Uuid evaluationId, Uuid deviationId,
        long expectedRevision, string disposition, string rationale,
        SeparationOfDutiesWaiver? waiver, Uuid actorMemberId, DateTimeOffset disposedAt)
    {
        if (Find(controlId, evaluationId) is not { } evaluation)
            return CommandFailure.MissingRecord("The control evaluation was not found.");
        if (Writable(evaluation, expectedRevision, actorMemberId) is { } blocked)
            return blocked;
        if (CurrentDeviations(evaluation).FirstOrDefault(d => d.DeviationId == deviationId) is not
            { } deviation)
            return CommandFailure.MissingRecord("The deviation is not a current deviation of this evaluation.");
        if (deviation.Status == Dispositioned)
            return CommandFailure.StateConflict("The deviation already has a disposition.");
        if (deviation.Classification != Minor)
            return CommandFailure.StateConflict(
                "A material deviation routes to a finding and a retest; it cannot be dispositioned.");
        if (disposition is not (Corrected or AcceptedWithWaiver) || !IsBoundedText(rationale) ||
            disposition == Corrected && waiver is not null ||
            disposition == AcceptedWithWaiver && waiver is null)
            return CommandFailure.InvalidContent(
                "A disposition is corrected, or accepted_with_waiver naming an approved waiver, with a rationale.");
        if (waiver is not null && (waiver.TenantId != _tenantId || !waiver.Allows(
                new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.ControlEvaluation,
                    evaluationId, deviationId, evaluation.Round, SeparationOfDutiesActions.Approve),
                actorMemberId, disposedAt)))
            return CommandFailure.StateConflict(
                "The waiver is not approved and active for this evaluator, deviation, and round.");
        RaiseEvent(new ControlEvaluationDeviationDisposed(_tenantId, Id, controlId, evaluationId,
            evaluation.Revision + 1, deviationId, disposition, rationale.Trim(), waiver?.Id));
        return null;
    }

    public CommandFailure? Submit(Uuid controlId, Uuid evaluationId, long expectedRevision,
        IReadOnlyList<EvaluationAssertionConclusion>? conclusions, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset submittedAt)
    {
        if (Find(controlId, evaluationId) is not { } evaluation)
            return CommandFailure.MissingRecord("The control evaluation was not found.");
        if (Writable(evaluation, expectedRevision, actorMemberId) is { } blocked)
            return blocked;
        var steps = Steps(evaluation);
        if (steps.Any(static step => step.Result is null))
            return CommandFailure.StateConflict("Record a result for every procedure step first.");
        var round = CurrentDeviations(evaluation);
        if (round.Any(static d => d.Status == PendingDisposition))
            return CommandFailure.StateConflict("Every minor deviation needs a disposition first.");
        if (conclusions is null || conclusions.Count != Assertions.Count ||
            conclusions.Any(static c => c is null || !IsBoundedText(c.Rationale)) ||
            Assertions.Any(assertion => conclusions.Count(c => c.Assertion == assertion) != 1))
            return CommandFailure.InvalidContent(
                "Conclude each of design, implementation, and evidence_sufficiency exactly once with a rationale.");
        foreach (var conclusion in conclusions)
        {
            var allowed = AllowedConclusions(steps.Where(s => s.Assertion == conclusion.Assertion)
                .ToArray(), round);
            if (!allowed.Contains(conclusion.Conclusion))
                return CommandFailure.InvalidContent(
                    $"The {conclusion.Assertion} conclusion must be one of {string.Join(", ", allowed)} given its step results and deviations.");
        }
        var clean = Assertions.Select(assertion => conclusions.Single(c => c.Assertion == assertion))
            .Select(static c => c with { Rationale = c.Rationale.Trim() }).ToArray();
        RaiseEvent(new ControlEvaluationSubmitted(_tenantId, Id, controlId, evaluationId,
            evaluation.Revision + 1, new EvaluationSubmissionView(evaluation.Round, clean,
                Overall(clean.Select(static c => c.Conclusion)), steps, round,
                ActorReference.ForMember(actorMemberId, actorDisplay), submittedAt)));
        return null;
    }

    /// <summary>
    ///     The conclusions the step results support. No steps or an untested step cannot be
    ///     concluded effective; a material deviation is ineffective; a minor one is at best
    ///     effective_with_exceptions. An evaluator may always conclude more severely.
    /// </summary>
    static string[] AllowedConclusions(ControlEvaluationStepView[] steps,
        IReadOnlyList<EvaluationDeviationView> deviations)
    {
        var stepIds = steps.Select(static s => s.StepId).ToHashSet();
        var related = deviations.Where(d => stepIds.Contains(d.StepId)).ToArray();
        if (steps.Length == 0)
            return [NotTested];
        if (related.Any(static d => d.Classification == Material))
            return [Ineffective];
        if (steps.Any(static s => s.Result!.Result == NotTested))
            return [NotTested, Ineffective];
        return related.Length > 0
            ? [EffectiveWithExceptions, Ineffective]
            : [Effective, EffectiveWithExceptions, Ineffective];
    }

    /// <summary>The overall result: ineffective dominates, then not_tested, then exceptions.</summary>
    public static string Overall(IEnumerable<string> conclusions)
    {
        var all = conclusions.ToArray();
        return all.Contains(Ineffective) ? Ineffective
            : all.Contains(NotTested) ? NotTested
            : all.Contains(EffectiveWithExceptions) ? EffectiveWithExceptions
            : Effective;
    }

    public CommandFailure? Review(Uuid controlId, Uuid evaluationId, long expectedRevision,
        Uuid decisionId, string decision, string rationale, Uuid reviewerMemberId,
        string reviewerDisplay, DateTimeOffset reviewedAt, SeparationOfDutiesWaiver? waiver)
    {
        if (Find(controlId, evaluationId) is not { } evaluation)
            return CommandFailure.MissingRecord("The control evaluation was not found.");
        if (evaluation.Reviews.Exists(review => review.DecisionId == decisionId))
            return null;
        if (expectedRevision != evaluation.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "control evaluation", evaluation.Revision));
        if (evaluation.State != Submitted)
            return CommandFailure.StateConflict("The evaluation has no submission awaiting review.");
        var scope = new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.ControlEvaluation, evaluationId, evaluationId,
            evaluation.Round, SeparationOfDutiesActions.Review);
        if (waiver is not null && (waiver.TenantId != _tenantId ||
                                   !waiver.Allows(scope, reviewerMemberId, reviewedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and evaluation round.");
        if (evaluation.Started.EvaluatorMemberId == reviewerMemberId && waiver is null)
            return CommandFailure.ActorProhibited(
                "The evaluator cannot review their own evaluation without an approved, active separation-of-duties waiver.");
        if (decision is not (Accepted or Rejected or ChangesRequested) || !IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "A review decision is accepted, rejected, or changes_requested with a rationale.");
        RaiseEvent(new ControlEvaluationReviewed(_tenantId, Id, controlId, evaluationId,
            evaluation.Revision + 1, new ControlEvaluationReviewView(decisionId, evaluation.Round,
                decision, rationale.Trim(), reviewerMemberId,
                ActorReference.ForMember(reviewerMemberId, reviewerDisplay), reviewedAt,
                waiver?.Id)));
        return null;
    }

    /// <summary>The submitted deviation in this program, or null when it was never submitted.</summary>
    public EvaluationDeviationView? SubmittedDeviation(Uuid controlId, Uuid evaluationId,
        Uuid deviationId) => Find(controlId, evaluationId)?.Submissions
        .SelectMany(static s => s.Deviations).LastOrDefault(d => d.DeviationId == deviationId);

    public ControlEvaluationView? Read(Uuid controlId, Uuid evaluationId) =>
        Find(controlId, evaluationId) is { } evaluation ? ToView(evaluation) : null;

    public IReadOnlyList<ControlEvaluationView> ReadControl(Uuid controlId) => _evaluations.Values
        .Where(evaluation => evaluation.Started.ControlId == controlId)
        .OrderByDescending(static evaluation => evaluation.Started.StartedAt)
        .ThenBy(static evaluation => evaluation.Started.EvaluationId.ToString(),
            StringComparer.Ordinal)
        .Select(ToView).ToArray();

    /// <summary>Current evaluation rounds awaiting independent review.</summary>
    public IReadOnlyList<ControlEvaluationView> ReadAwaitingReview() => _evaluations.Values
        .Where(static evaluation => evaluation.State == Submitted)
        .OrderBy(static evaluation => evaluation.Started.StartedAt)
        .ThenBy(static evaluation => evaluation.Started.EvaluationId.ToString(),
            StringComparer.Ordinal)
        .Select(ToView).ToArray();

    ControlEvaluationView ToView(EvaluationState evaluation)
    {
        var started = evaluation.Started;
        var retests = _evaluations.Values
            .Where(other => other.Started.RetestOfEvaluationId == started.EvaluationId)
            .OrderBy(static other => other.Started.StartedAt).ToArray();
        var accepted = AcceptedSubmission(evaluation);
        string retestStatus;
        if (accepted is null || !accepted.Deviations.Any(static d => d.Classification == Material))
            retestStatus = NotRequired;
        else if (retests.Length == 0)
            retestStatus = Required;
        else
            retestStatus = retests.Where(static retest => retest.State == Accepted)
                .OrderBy(static retest => retest.Reviews[^1].ReviewedAt)
                .Select(static retest => AcceptedSubmission(retest)!.Overall)
                .LastOrDefault() switch
            {
                null => InProgress,
                Effective or EffectiveWithExceptions => Passed,
                _ => Failed,
            };
        return new ControlEvaluationView(_tenantId, Id, started.ControlId, started.EvaluationId,
            evaluation.Revision, started.ControlVersionId, evaluation.State, evaluation.Round,
            started.EvaluatorMemberId, started.StartedBy, started.StartedAt, Steps(evaluation),
            evaluation.Deviations.ToArray(), evaluation.Submissions.ToArray(),
            evaluation.Reviews.ToArray(), evaluation.Reviews.LastOrDefault(), accepted?.Overall,
            started.RetestOfEvaluationId, started.RetestOfDeviationIds, retestStatus,
            retests.Select(static other => other.Started.EvaluationId).ToArray())
        {
            PlanVersionId = started.PlanVersionId,
            PlanVersion = started.PlanVersion,
        };
    }

    static EvaluationSubmissionView? AcceptedSubmission(EvaluationState evaluation) =>
        evaluation.State == Accepted ? evaluation.Submissions[^1] : null;

    static ControlEvaluationStepView[] Steps(EvaluationState evaluation) =>
        evaluation.Started.Steps.Select((step, index) =>
        {
            var stepId = StepId(evaluation.Started.EvaluationId, index);
            return new ControlEvaluationStepView(stepId, index, step.Assertion, step.Method,
                step.InspectedItems, step.ExpectedCondition,
                evaluation.Results.GetValueOrDefault(stepId));
        }).ToArray();

    /// <summary>The latest deviation of each step whose current result is not_met.</summary>
    static EvaluationDeviationView[] CurrentDeviations(EvaluationState evaluation) =>
        evaluation.Results.Where(static result => result.Value.Result == NotMet)
            .Select(result => evaluation.Deviations.Last(d => d.StepId == result.Key))
            .OrderBy(static d => d.DetectedAt).ToArray();

    public static Uuid StepId(Uuid evaluationId, int index) => Uuid.CreateVersion5(evaluationId,
        "step-" + index.ToString(CultureInfo.InvariantCulture));

    static int StepIndex(EvaluationState evaluation, Uuid stepId)
    {
        for (var index = 0; index < evaluation.Started.Steps.Count; index++)
            if (StepId(evaluation.Started.EvaluationId, index) == stepId)
                return index;
        return -1;
    }

    EvaluationState? Find(Uuid controlId, Uuid evaluationId) =>
        _evaluations.TryGetValue(evaluationId, out var evaluation) &&
        evaluation.Started.ControlId == controlId
            ? evaluation
            : null;

    static CommandFailure? Writable(EvaluationState evaluation, long expectedRevision,
        Uuid actorMemberId)
    {
        if (evaluation.Started.EvaluatorMemberId != actorMemberId)
            return CommandFailure.ActorProhibited("Only the evaluator may record this evaluation.");
        if (expectedRevision != evaluation.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "control evaluation", evaluation.Revision));
        return evaluation.State != InProgress
            ? CommandFailure.StateConflict("The evaluation is not open for evaluator work.")
            : null;
    }

    static bool AreItems(IReadOnlyList<EvaluationInspectedItem>? items, bool required) =>
        items is not null && (!required || items.Count > 0) && items.Count <= MaximumItems &&
        items.All(static item => item is not null && (item.Kind is "record" or "artifact" or
            "boundary" or "commitment" or "risk" or "criterion" or "control" or "policy" or
            "provider" or "evidence") && !string.IsNullOrWhiteSpace(item.Reference) &&
            item.Reference.Trim().Length <= 2000 && !string.IsNullOrWhiteSpace(item.Version) &&
            item.Version.Trim().Length <= 200);

    static EvaluationInspectedItem Clean(EvaluationInspectedItem item) =>
        new(item.Kind, item.Reference.Trim(), item.Version.Trim());

    static EvaluationProcedureStep Clean(EvaluationProcedureStep step) => new(step.Assertion,
        step.Method, step.InspectedItems.Select(Clean).ToArray(), step.ExpectedCondition.Trim());

    static bool IsBoundedText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= MaximumTextLength;

    sealed class EvaluationState(ControlEvaluationStarted started)
    {
        public ControlEvaluationStarted Started { get; } = started;
        public long Revision { get; set; } = started.Revision;
        public string State { get; set; } = InProgress;
        public int Round { get; set; } = 1;
        public Dictionary<Uuid, EvaluationStepResultView> Results { get; } = [];
        public List<EvaluationDeviationView> Deviations { get; } = [];
        public List<EvaluationSubmissionView> Submissions { get; } = [];
        public List<ControlEvaluationReviewView> Reviews { get; } = [];
    }
}
