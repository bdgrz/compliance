using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     A risk's assessments, chosen treatment, and personal acceptances under M0-D10.
///     It shares the risk's identity but keeps its own revision sequence.
/// </summary>
public sealed class RiskEvaluation : Aggregate
{
    public const string Inherent = "inherent";
    public const string Target = "target";
    public const string Residual = "residual";
    public const string ComplianceLead = "compliance_lead";
    public const string Executive = "executive";
    const int MaximumRationaleLength = 4000;
    static readonly string[] Phases = [Inherent, Target, Residual];
    static readonly string[] TreatmentKinds = ["mitigate", "accept", "transfer", "avoid"];

    readonly Uuid _tenantId;
    readonly List<RiskAssessmentView> _assessments = [];
    readonly List<RiskAcceptanceView> _acceptances = [];
    ActorReference? _lastChangedBy;
    DateTimeOffset? _lastChangedAt;

    public long Revision { get; private set; }
    public Uuid ProgramId { get; private set; }
    public RiskTreatmentView? Treatment { get; private set; }

    public RiskEvaluation(Uuid tenantId, Uuid riskId)
        : base(riskId, new EventStreamAddress(tenantId.ToString(), "risk-evaluations",
            riskId.ToString()))
    {
        _tenantId = tenantId;
        On<RiskAssessmentRecorded>(ev =>
        {
            Apply(ev.ProgramId, ev.Revision, ev.Assessment.Assessor, ev.Assessment.AssessedAt);
            _assessments.Add(ev.Assessment);
        });
        On<RiskTreatmentChosen>(ev =>
        {
            Apply(ev.ProgramId, ev.Revision, ev.Treatment.ChosenBy, ev.Treatment.ChosenAt);
            Treatment = ev.Treatment;
        });
        On<RiskAccepted>(ev =>
        {
            Apply(ev.ProgramId, ev.Revision, ev.Acceptance.Approver, ev.Acceptance.AcceptedAt);
            _acceptances.Add(ev.Acceptance);
        });
    }

    public RiskAssessmentView? Latest(string phase) =>
        _assessments.LastOrDefault(assessment => assessment.Phase == phase);

    public RiskAssessmentView? FindAssessment(Uuid assessmentId) =>
        _assessments.Find(assessment => assessment.AssessmentId == assessmentId);

    public RiskAcceptanceView? FindAcceptance(Uuid acceptanceId) =>
        _acceptances.Find(acceptance => acceptance.AcceptanceId == acceptanceId);

    public CommandFailure? RecordAssessment(Uuid programId, long expectedRevision,
        Uuid assessmentId, RiskMethodVersionView method, string phase, int likelihood,
        int impact, string rationale, Uuid assessorMemberId, string assessorDisplay,
        DateTimeOffset assessedAt, bool hasAcceptedControlTreatment = false)
    {
        ArgumentNullException.ThrowIfNull(method);
        if (FindAssessment(assessmentId) is { } existing)
            return existing.Phase == phase && existing.Likelihood == likelihood &&
                   existing.Impact == impact && existing.MethodVersionId == method.MethodVersionId
                ? null
                : CommandFailure.StateConflict("The assessment request was already recorded with different content.");
        var scope = CheckScope(programId, expectedRevision);
        if (scope is not null)
            return scope;
        if (method.TenantId != _tenantId || method.ProgramId != programId)
            return CommandFailure.InvalidContent("The risk method version belongs to another program.");
        if (!Phases.Contains(phase))
            return CommandFailure.InvalidContent("The assessment phase must be inherent, target, or residual.");
        if (likelihood is < 1 or > 5 || impact is < 1 or > 5)
            return CommandFailure.InvalidContent("Likelihood and impact must each be from 1 to 5.");
        if (InvalidRationale(rationale))
            return CommandFailure.InvalidContent("An assessment requires a rationale of at most 4000 characters.");
        if (phase != Inherent)
        {
            if (Latest(Inherent) is not { } inherent)
                return CommandFailure.StateConflict("Record an inherent assessment first.");
            if (inherent.MethodVersionId != method.MethodVersionId)
                return CommandFailure.StateConflict(
                    "The method changed since the inherent assessment. Reassess the inherent risk first.");
        }
        if (phase == Residual && Treatment is null)
            return CommandFailure.StateConflict(
                "A residual assessment requires a chosen treatment.");
        if (phase == Residual && Treatment is { Kind: "mitigate" } && !hasAcceptedControlTreatment)
            return CommandFailure.StateConflict(
                "A residual assessment under mitigate requires an independently accepted control treatment.");
        RaiseEvent(new RiskAssessmentRecorded(_tenantId, programId, Id, Revision + 1,
            new RiskAssessmentView(assessmentId, phase, method.MethodVersionId, method.Version,
                likelihood, impact, likelihood * impact, rationale.Trim(),
                ActorReference.ForMember(assessorMemberId, assessorDisplay), assessedAt)));
        return null;
    }

    public CommandFailure? ChooseTreatment(Uuid programId, long expectedRevision, string kind,
        string rationale, Uuid actorMemberId, string actorDisplay, DateTimeOffset chosenAt)
    {
        var scope = CheckScope(programId, expectedRevision);
        if (scope is not null)
            return scope;
        if (!TreatmentKinds.Contains(kind))
            return CommandFailure.InvalidContent("The treatment must be mitigate, accept, transfer, or avoid.");
        if (InvalidRationale(rationale))
            return CommandFailure.InvalidContent("A treatment requires a rationale of at most 4000 characters.");
        if (Latest(Inherent) is null)
            return CommandFailure.StateConflict("Record an inherent assessment before choosing a treatment.");
        RaiseEvent(new RiskTreatmentChosen(_tenantId, programId, Id, Revision + 1,
            new RiskTreatmentView(kind, rationale.Trim(),
                ActorReference.ForMember(actorMemberId, actorDisplay), chosenAt)));
        return null;
    }

    public CommandFailure? Accept(Uuid programId, long expectedRevision, Uuid acceptanceId,
        Uuid residualAssessmentId, RiskMethodVersionView method, string authority,
        bool approverHoldsAuthority, DateTimeOffset expiresAt, string rationale,
        Uuid approverMemberId, string approverDisplay, DateTimeOffset acceptedAt,
        Uuid? ownerMemberId = null, SeparationOfDutiesWaiver? waiver = null)
    {
        ArgumentNullException.ThrowIfNull(method);
        if (FindAcceptance(acceptanceId) is { } existing)
            return existing.ResidualAssessmentId == residualAssessmentId &&
                   existing.ApproverMemberId == approverMemberId
                ? null
                : CommandFailure.StateConflict("The acceptance request was already recorded with different content.");
        var scope = CheckScope(programId, expectedRevision);
        if (scope is not null)
            return scope;
        if (authority is not (ComplianceLead or Executive))
            return CommandFailure.InvalidContent("The approver authority must be compliance_lead or executive.");
        if (InvalidRationale(rationale))
            return CommandFailure.InvalidContent("An acceptance requires a rationale of at most 4000 characters.");
        if (expiresAt <= acceptedAt || expiresAt > acceptedAt.AddMonths(12))
            return CommandFailure.InvalidContent("An acceptance must expire within 12 months.");
        if (Treatment is not { Kind: "accept" })
            return CommandFailure.StateConflict("Only a risk whose chosen treatment is accept can be accepted.");
        if (Latest(Residual) is not { } residual || residual.AssessmentId != residualAssessmentId)
            return CommandFailure.StateConflict("Accept the current residual assessment.");
        if (method.MethodVersionId != residual.MethodVersionId)
            return CommandFailure.StateConflict("The residual assessment uses another method version.");
        if (!approverHoldsAuthority)
            return CommandFailure.ActorProhibited($"The approver does not hold {authority} acceptance authority.");
        if (authority == ComplianceLead &&
            (method.AppetiteThreshold is not { } appetite || residual.Score > appetite))
            return CommandFailure.ActorProhibited(
                "Accepting a risk above appetite, or while appetite is unset, requires an executive approver.");
        if (residual.Assessor.Kind == "member" &&
            residual.Assessor.Id == approverMemberId.ToString())
            return CommandFailure.ActorProhibited("The residual assessor cannot accept the same risk.");
        if (waiver is not null && (waiver.TenantId != _tenantId || !waiver.Allows(
                new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.Risk, Id,
                    residualAssessmentId, expectedRevision, SeparationOfDutiesActions.Approve),
                approverMemberId, acceptedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and risk revision.");
        if (ownerMemberId == approverMemberId && waiver is null)
            return CommandFailure.ActorProhibited(
                "The risk owner cannot accept their own risk without a separation-of-duties waiver.");
        RaiseEvent(new RiskAccepted(_tenantId, programId, Id, Revision + 1,
            new RiskAcceptanceView(acceptanceId, residualAssessmentId, residual.Score,
                method.AppetiteThreshold, approverMemberId,
                ActorReference.ForMember(approverMemberId, approverDisplay), authority,
                rationale.Trim(), acceptedAt, expiresAt)
            {
                SeparationOfDutiesWaiverId = waiver?.Id,
            }));
        return null;
    }

    public RiskEvaluationView ToView() => new(_tenantId, ProgramId, Id, Revision, "unassessed",
        [.. _assessments], Treatment, [.. _acceptances], null, _lastChangedBy, _lastChangedAt);

    CommandFailure? CheckScope(Uuid programId, long expectedRevision)
    {
        if (Revision > 0 && ProgramId != programId)
            return CommandFailure.MissingRecord("The risk was not found.");
        return expectedRevision != Revision
            ? CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("risk evaluation",
                Revision))
            : null;
    }

    void Apply(Uuid programId, long revision, ActorReference actor, DateTimeOffset at)
    {
        ProgramId = programId;
        Revision = revision;
        _lastChangedBy = actor;
        _lastChangedAt = at;
    }

    static bool InvalidRationale(string? rationale) =>
        string.IsNullOrWhiteSpace(rationale) || rationale.Trim().Length > MaximumRationaleLength;
}
