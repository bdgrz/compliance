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
    public const string Proceed = "proceed";
    public const string DoNotProceed = "do_not_proceed";
    const int MaximumTextLength = 4000;

    readonly Uuid _tenantId;
    readonly List<AssessmentState> _assessments = [];
    readonly Dictionary<Uuid, ReadinessGapPlanView> _plans = [];

    public ReadinessLedger(Uuid tenantId, Uuid programId)
        : base(programId, new EventStreamAddress(tenantId.ToString(), "readiness",
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
            gaps.Length, ev.RunBy, ev.RunAt, assessment.Decision);
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
