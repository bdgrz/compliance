using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     One program's readiness assessments, gap plan, and management decisions in one stream.
///     Assessments are immutable once recorded; the gap plan is keyed by stable gap identity so
///     it carries across reassessments; each assessment accepts at most one decision.
/// </summary>
public sealed class ReadinessLedger : Aggregate
{
    public const string Area = "readiness";
    public const string Proceed = "proceed";
    public const string DoNotProceed = "do_not_proceed";
    public const string Approve = "approve";
    public const string ApproveWithExceptions = "approve_with_exceptions";
    public const string Defer = "defer";
    const int MaximumTextLength = 4000;

    readonly Uuid _tenantId;
    readonly List<AssessmentState> _assessments = [];
    readonly Dictionary<Uuid, ReadinessGapPlanView> _plans = [];
    readonly List<TypeIEntryDecisionView> _entryDecisions = [];
    readonly List<ReadinessAnnotationView> _annotations = [];

    public ReadinessLedger(Uuid tenantId, Uuid programId)
        : base(programId, new EventStreamAddress(tenantId.ToString(), Area,
            programId.ToString()))
    {
        _tenantId = tenantId;
        On<ReadinessAssessmentRecorded>(ev =>
        {
            Revision = ev.Revision;
            _assessments.Add(new AssessmentState(ev));
        });
        On<ReadinessGapPlanned>(ev =>
        {
            Revision = ev.Revision;
            _plans[ev.Plan.GapId] = ev.Plan;
        });
        On<ReadinessDecisionRecorded>(ev =>
        {
            Revision = ev.Revision;
            Find(ev.Decision.AssessmentId)!.Decision = ev.Decision;
        });
        On<TypeIEntryDecisionRecorded>(ev =>
        {
            Revision = ev.Revision;
            _entryDecisions.Add(ev.Decision);
        });
        On<ReadinessGapAnnotated>(ev =>
        {
            Revision = ev.Revision;
            _annotations.Add(ev.Annotation);
        });
    }

    public long Revision { get; private set; }

    public CommandFailure? Record(long expectedRevision, Uuid assessmentId, DateTimeOffset asOf,
        Uuid? editionId, ReadinessEvaluation evaluation, Uuid runnerMemberId,
        string runnerDisplay, DateTimeOffset runAt)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        if (Find(assessmentId) is { } existing)
            return existing.Recorded.RunnerMemberId == runnerMemberId
                ? null
                : CommandFailure.StateConflict("The assessment request was already recorded.");
        if (expectedRevision != Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "readiness ledger", Revision));
        RaiseEvent(new ReadinessAssessmentRecorded(_tenantId, Id, Revision + 1, assessmentId,
            ReadinessRules.Version, asOf, editionId, evaluation.InputFingerprint,
            evaluation.Inputs, evaluation.Findings, evaluation.Gaps, runnerMemberId,
            ActorReference.ForMember(runnerMemberId, runnerDisplay), runAt));
        return null;
    }

    public CommandFailure? Plan(Uuid gapId, long expectedRevision, Uuid ownerMemberId,
        DateOnly targetDate, string action, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset plannedAt)
    {
        if (!_assessments.Any(assessment => assessment.Recorded.Gaps.Any(gap =>
                gap.GapId == gapId)))
            return CommandFailure.MissingRecord("The readiness gap was not found.");
        if (expectedRevision != Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "readiness ledger", Revision));
        if (ownerMemberId == Uuid.Empty || targetDate == default || !IsBoundedText(action))
            return CommandFailure.InvalidContent(
                "A gap plan requires an owner, a target date, and an action of at most 4000 characters.");
        if (targetDate < DateOnly.FromDateTime(plannedAt.UtcDateTime))
            return CommandFailure.InvalidContent("A gap plan target date cannot be in the past.");
        RaiseEvent(new ReadinessGapPlanned(_tenantId, Id, Revision + 1,
            new ReadinessGapPlanView(gapId, ownerMemberId, targetDate, action.Trim(),
                ActorReference.ForMember(actorMemberId, actorDisplay), plannedAt)));
        return null;
    }

    /// <summary>
    ///     The runner may decide only under an approved waiver scoped to
    ///     (readiness_assessment, assessment_id, assessment_id, expected_revision, approve).
    /// </summary>
    public CommandFailure? Decide(Uuid assessmentId, long expectedRevision, Uuid decisionId,
        string outcome, string rationale, Uuid deciderMemberId, string deciderDisplay,
        DateTimeOffset decidedAt, SeparationOfDutiesWaiver? waiver = null)
    {
        if (Find(assessmentId) is not { } assessment)
            return CommandFailure.MissingRecord("The readiness assessment was not found.");
        if (assessment.Decision is { } existing)
            return existing.DecisionId == decisionId
                ? null
                : CommandFailure.StateConflict("The readiness assessment was already decided.");
        if (expectedRevision != Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "readiness ledger", Revision));
        if (_assessments[^1] != assessment)
            return CommandFailure.StateConflict(
                "A later assessment exists; decide the latest assessment.");
        if (waiver is not null && (waiver.TenantId != _tenantId || !waiver.Allows(
                new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.ReadinessAssessment,
                    assessmentId, assessmentId, expectedRevision, SeparationOfDutiesActions.Approve),
                deciderMemberId, decidedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and assessment revision.");
        if (assessment.Recorded.RunnerMemberId == deciderMemberId && waiver is null)
            return CommandFailure.ActorProhibited(
                "The member who ran an assessment cannot decide it.");
        if (outcome is not (Proceed or DoNotProceed) || !IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "A readiness decision requires an outcome of proceed or do_not_proceed and a rationale of at most 4000 characters.");
        if (outcome == Proceed && assessment.Recorded.Gaps.Any(gap =>
                !_plans.ContainsKey(gap.GapId)))
            return CommandFailure.StateConflict(
                "Every gap needs an owner and target date before management can proceed.");
        RaiseEvent(new ReadinessDecisionRecorded(_tenantId, Id, Revision + 1,
            new ReadinessDecisionView(decisionId, assessmentId, outcome, rationale.Trim(),
                deciderMemberId, ActorReference.ForMember(deciderMemberId, deciderDisplay),
                decidedAt, waiver?.Id)));
        return null;
    }

    /// <summary>
    ///     The Type I entry sign-off on the latest assessment. Approval requires that management
    ///     decided to proceed on it, the current rule version, and that no earlier decision
    ///     already entered Type I. approve requires no gaps; approve_with_exceptions requires every
    ///     gap to be planned and explicitly acknowledged. defer always records.
    ///     The runner may sign only under an approved waiver scoped to
    ///     (type_i_entry_decision, assessment_id, assessment_id, expected_revision, approve).
    /// </summary>
    public CommandFailure? DecideTypeIEntry(Uuid assessmentId, long expectedRevision,
        Uuid decisionId, string outcome, string rationale, IReadOnlyCollection<Uuid> acknowledged,
        Uuid deciderMemberId, string deciderDisplay, DateTimeOffset decidedAt,
        SeparationOfDutiesWaiver? waiver = null)
    {
        ArgumentNullException.ThrowIfNull(acknowledged);
        if (_entryDecisions.Find(decision => decision.DecisionId == decisionId) is { } retry)
            return retry.AssessmentId == assessmentId && retry.DeciderMemberId == deciderMemberId
                ? null
                : CommandFailure.StateConflict("The Type I entry request was already recorded.");
        if (Find(assessmentId) is not { } assessment)
            return CommandFailure.MissingRecord("The readiness assessment was not found.");
        if (expectedRevision != Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "readiness ledger", Revision));
        if (_entryDecisions.Any(static decision => decision.Outcome != Defer))
            return CommandFailure.StateConflict("The program already entered Type I.");
        if (_entryDecisions.Any(decision => decision.AssessmentId == assessmentId))
            return CommandFailure.StateConflict(
                "The assessment already has a Type I entry decision; run a new assessment.");
        if (_assessments[^1] != assessment)
            return CommandFailure.StateConflict(
                "A later assessment exists; decide Type I entry on the latest assessment.");
        if (waiver is not null && (waiver.TenantId != _tenantId || !waiver.Allows(
                new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.TypeIEntryDecision,
                    assessmentId, assessmentId, expectedRevision, SeparationOfDutiesActions.Approve),
                deciderMemberId, decidedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and assessment revision.");
        if (assessment.Recorded.RunnerMemberId == deciderMemberId && waiver is null)
            return CommandFailure.ActorProhibited(
                "The member who ran an assessment cannot sign its Type I entry decision.");
        if (outcome is not (Approve or ApproveWithExceptions or Defer) || !IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "A Type I entry decision requires an outcome of approve, approve_with_exceptions, or defer and a rationale of at most 4000 characters.");
        var gaps = assessment.Recorded.Gaps;
        var gapIds = gaps.Select(static gap => gap.GapId).ToHashSet();
        if (acknowledged.Any(id => !gapIds.Contains(id)))
            return CommandFailure.InvalidContent(
                "Acknowledgements may name only gaps of this assessment.");
        if (outcome != Defer)
        {
            if (assessment.Recorded.RuleVersion != ReadinessRules.Version)
                return CommandFailure.StateConflict(
                    "The assessment used superseded readiness rules; run a new assessment.");
            if (assessment.Decision?.Outcome != Proceed)
                return CommandFailure.StateConflict(
                    "Management must decide to proceed on the assessment before Type I entry is approved.");
            if (outcome == Approve && gaps.Count > 0)
                return CommandFailure.StateConflict(
                    "Unresolved gaps remain; approve with exceptions and acknowledge each one, or defer.");
            if (outcome == ApproveWithExceptions)
            {
                if (gaps.Count == 0)
                    return CommandFailure.InvalidContent(
                        "No gaps remain; approve without exceptions.");
                if (!gapIds.SetEquals(acknowledged))
                    return CommandFailure.StateConflict(
                        "Every unresolved gap must be explicitly acknowledged.");
                if (gaps.Any(gap => !_plans.ContainsKey(gap.GapId)))
                    return CommandFailure.StateConflict(
                        "Every acknowledged gap needs an owner and target date.");
            }
        }
        var acknowledgedSet = acknowledged.ToHashSet();
        var items = gaps.Select(gap =>
        {
            var plan = _plans.GetValueOrDefault(gap.GapId);
            return new TypeIEntryUnresolvedItemView(gap.GapId, gap.Kind, gap.Subject, gap.RuleId,
                gap.Explanation, plan?.OwnerMemberId, plan?.TargetDate,
                acknowledgedSet.Contains(gap.GapId));
        }).ToArray();
        var recorded = assessment.Recorded;
        RaiseEvent(new TypeIEntryDecisionRecorded(_tenantId, Id, Revision + 1,
            new TypeIEntryDecisionView(decisionId, assessmentId, recorded.RuleVersion,
                recorded.AsOf, recorded.InputFingerprint, outcome, rationale.Trim(), items,
                deciderMemberId, ActorReference.ForMember(deciderMemberId, deciderDisplay),
                decidedAt, waiver?.Id)));
        return null;
    }

    /// <summary>Records immutable, attributed feedback on one gap of the latest assessment.</summary>
    public CommandFailure? Annotate(Uuid assessmentId, Uuid gapId, long expectedRevision,
        Uuid annotationId, string body, Uuid authorMemberId, string authorDisplay,
        DateTimeOffset annotatedAt)
    {
        if (_annotations.Find(annotation => annotation.AnnotationId == annotationId) is { } retry)
            return retry.GapId == gapId && retry.AuthorMemberId == authorMemberId
                ? null
                : CommandFailure.StateConflict("The annotation request was already recorded.");
        if (Find(assessmentId) is not { } assessment ||
            assessment.Recorded.Gaps.All(gap => gap.GapId != gapId))
            return CommandFailure.MissingRecord("The readiness gap was not found.");
        if (expectedRevision != Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "readiness ledger", Revision));
        if (_assessments[^1] != assessment)
            return CommandFailure.StateConflict(
                "A later assessment exists; annotate the latest assessment.");
        if (!IsBoundedText(body))
            return CommandFailure.InvalidContent(
                "An annotation requires a body of at most 4000 characters.");
        RaiseEvent(new ReadinessGapAnnotated(_tenantId, Id, Revision + 1,
            new ReadinessAnnotationView(annotationId, assessmentId, gapId, body.Trim(),
                authorMemberId, ActorReference.ForMember(authorMemberId, authorDisplay),
                annotatedAt)));
        return null;
    }

    public TypeIEntryDecisionView? FindTypeIEntryDecision(Uuid decisionId) =>
        _entryDecisions.Find(decision => decision.DecisionId == decisionId);

    public ReadinessAnnotationView? FindAnnotation(Uuid annotationId) =>
        _annotations.Find(annotation => annotation.AnnotationId == annotationId);

    public IReadOnlyList<TypeIEntryDecisionView> TypeIEntryDecisions() =>
        _entryDecisions.AsEnumerable().Reverse().ToArray();

    /// <summary>Annotations on one assessment; null when the assessment does not exist.</summary>
    public IReadOnlyList<ReadinessAnnotationView>? Annotations(Uuid assessmentId)
    {
        if (Find(assessmentId) is null)
            return null;
        var current = _assessments[^1].Recorded.AssessmentId == assessmentId;
        return _annotations.Where(annotation => annotation.AssessmentId == assessmentId)
            .Select(annotation => annotation with { Current = current }).ToArray();
    }

    public ReadinessGapPlanView? FindPlan(Uuid gapId) => _plans.GetValueOrDefault(gapId);

    public ReadinessDecisionView? FindDecision(Uuid assessmentId) => Find(assessmentId)?.Decision;

    public ReadinessAssessmentView? Read(Uuid assessmentId)
    {
        if (Find(assessmentId) is not { } assessment)
            return null;
        var ev = assessment.Recorded;
        var gaps = ReadGaps(ev);
        return new ReadinessAssessmentView(_tenantId, Id, ev.AssessmentId, Revision,
            ev.RuleVersion, ev.AsOf, ev.EditionId, ev.InputFingerprint, ev.Inputs, ev.Findings,
            gaps, ev.Findings.Count(static finding => finding.Outcome == ReadinessRules.RuleMet),
            gaps.Length, ev.RunBy, ev.RunAt, assessment.Decision,
            _entryDecisions.Find(decision => decision.AssessmentId == assessmentId));
    }

    public IReadOnlyList<ReadinessGapView>? ReadGaps(Uuid assessmentId) =>
        Find(assessmentId) is { } assessment ? ReadGaps(assessment.Recorded) : null;

    public IReadOnlyList<ReadinessAssessmentSummaryView> Summaries() =>
        _assessments.AsEnumerable().Reverse().Select(static assessment =>
        {
            var ev = assessment.Recorded;
            return new ReadinessAssessmentSummaryView(ev.AssessmentId, ev.RuleVersion, ev.AsOf,
                ev.Findings.Count(static finding => finding.Outcome == ReadinessRules.RuleMet),
                ev.Gaps.Count, ev.RunBy, ev.RunAt, assessment.Decision?.Outcome);
        }).ToArray();

    ReadinessGapView[] ReadGaps(ReadinessAssessmentRecorded ev) =>
        ev.Gaps.Select(gap => gap with { Plan = _plans.GetValueOrDefault(gap.GapId) }).ToArray();

    AssessmentState? Find(Uuid assessmentId) =>
        _assessments.Find(assessment => assessment.Recorded.AssessmentId == assessmentId);

    static bool IsBoundedText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= MaximumTextLength;

    sealed class AssessmentState(ReadinessAssessmentRecorded recorded)
    {
        public ReadinessAssessmentRecorded Recorded { get; } = recorded;
        public ReadinessDecisionView? Decision { get; set; }
    }
}
