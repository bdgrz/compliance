using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed partial class IndependenceLedger
{
    readonly List<PartnerIndependenceEvaluationView> _partnerEvaluations = [];

    public IReadOnlyList<PartnerIndependenceEvaluationView> PartnerEvaluations =>
        Array.AsReadOnly(_partnerEvaluations.ToArray());

    public IReadOnlyList<PartnerIndependenceEvaluationView> PartnerEvaluationsFor(Uuid engagementId) =>
        Array.AsReadOnly(_partnerEvaluations.Where(item => item.EngagementId == engagementId).ToArray());

    internal Result<PartnerIndependenceEvaluationView> RecordPartnerIndependenceEvaluation(Uuid requestId,
        RecordPartnerIndependenceEvaluation request, IndependenceRuleVersionView ratifiedRules,
        CurrentProfessionalDuty currentPartner, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (request is null || ratifiedRules is null || currentPartner is null ||
            request.TenantId != _tenantId || request.TenantId == Uuid.Empty || request.EngagementId == Uuid.Empty ||
            request.EvaluationId == Uuid.Empty || request.ExpectedEngagementRevision <= 0 || request.RuleVersion <= 0 ||
            !ValidDigest(request.ExpectedRuleContentDigest) || !ValidDigest(request.ExpectedServiceHistoryDigest) ||
            !Bounded(request.Rationale, 2000) || !IndependenceRecordValidation.ValidAttribution(requestId, actor,
                recordedAt, "firm_staff") || !ValidRatifiedRules(ratifiedRules) ||
            currentPartner.Staff is not { IsActive: true, Revision: > 0 } staff ||
            currentPartner.Designation is not { IsActive: true, Duty: FirmProfessionalDuty.EngagementPartner } duty ||
            duty.TenantId != _tenantId || duty.UserId != staff.UserId || duty.StaffMemberId != staff.StaffMemberId ||
            duty.DirectoryStaffRevision != staff.Revision || duty.Revision <= 0 ||
            duty.DesignatedAt > recordedAt || staff.RecordedAt > recordedAt ||
            ratifiedRules.RecordedAt > recordedAt || ratifiedRules.Ratification?.RecordedAt > recordedAt ||
            actor.Id != staff.UserId.ToString())
            return Failure<PartnerIndependenceEvaluationView>(RequestErrorKind.Validation,
                "A partner evaluation requires current tenant-scoped professional authority, ratified rules and bounded evidence.");

        if (Retry<PartnerIndependenceEvaluationView>(requestId, request, actor) is { } retry)
            return retry;
        if (request.ExpectedSequence != Sequence || _partnerEvaluations.Count >= 100 ||
            _partnerEvaluations.Any(item => item.EvaluationId == request.EvaluationId) ||
            Engagement(request.EngagementId) is not { Status: "draft" } engagement ||
            engagement.Revision != request.ExpectedEngagementRevision || ratifiedRules.Version != request.RuleVersion ||
            IndependenceSourceDigest.RuleContent(ratifiedRules.Content) != request.ExpectedRuleContentDigest ||
            IndependenceSourceDigest.Services(_services) != request.ExpectedServiceHistoryDigest ||
            !MatchingManagementAcknowledgement(engagement, staff.UserId, recordedAt))
            return Failure<PartnerIndependenceEvaluationView>(RequestErrorKind.Conflict,
                "The draft, management acknowledgement, ratified rules or complete client service history changed; reload before evaluation.");

        var policy = RuleSet(ratifiedRules);
        var sources = ServiceRecords();
        var decision = IndependenceCompartments.CanAcceptAttestEngagement(_tenantId, policy, sources,
            engagement.Content.PeriodStart, partnerEvaluationRecorded: false);
        if (decision.Code != IndependenceDecisionCode.PartnerEvaluationRequired ||
            decision.EvaluationOutcome != IndependenceEvaluationOutcome.ConditionallyCompatible)
            return Failure<PartnerIndependenceEvaluationView>(RequestErrorKind.Conflict,
                "Partner evaluation is available only for a fully classified conditionally compatible service history; impairment and unclassified services remain blocked.");

        var considered = decision.ConsideredServices.Select(item => item.Service).ToHashSet();
        var evaluation = new PartnerIndependenceEvaluationView(request.EvaluationId, _tenantId,
            engagement.EngagementId, engagement.Revision, ratifiedRules.Version,
            request.ExpectedRuleContentDigest, request.ExpectedServiceHistoryDigest,
            Array.AsReadOnly(_services.Select(IndependenceRecordValidation.Freeze).ToArray()),
            Array.AsReadOnly(_services.Where((_, index) => considered.Contains(sources[index]))
                .Select(item => item.ServiceRecordId).ToArray()), "partner_evaluation_required",
            "conditionally_compatible", request.Rationale, staff.StaffMemberId, staff.UserId, staff.Revision,
            duty.DesignationId, duty.Revision, duty.SourceReference, actor, recordedAt);
        var frozenRules = IndependenceRecordValidation.Freeze(ratifiedRules);
        var ev = new PartnerIndependenceEvaluationRecorded(requestId, Sequence, evaluation, frozenRules);
        if (!Fits(ev))
            return Failure<PartnerIndependenceEvaluationView>(RequestErrorKind.Validation,
                "The complete partner evaluation exceeds the bounded event payload; sources were not truncated.");
        RaiseEvent(ev);
        return Result<PartnerIndependenceEvaluationView>.Success(evaluation);
    }

    void Apply(PartnerIndependenceEvaluationRecorded ev)
    {
        var item = ev.Evaluation;
        if (ev.ExpectedSequence != Sequence || item.TenantId != _tenantId || item.EngagementId == Uuid.Empty ||
            item.EvaluationId == Uuid.Empty || item.DraftRevision <= 0 || item.RuleVersion <= 0 ||
            !ValidDigest(item.RuleContentDigest) || !ValidDigest(item.ServiceHistoryDigest) ||
            !Bounded(item.Rationale, 2000) || item.DecisionCode != "partner_evaluation_required" ||
            item.Outcome != "conditionally_compatible" || item.PartnerStaffMemberId == Uuid.Empty ||
            item.PartnerUserId == Uuid.Empty || item.PartnerDirectoryStaffRevision <= 0 ||
            item.DutyDesignationId == Uuid.Empty || item.PartnerDutyRevision <= 0 ||
            !Bounded(item.DutySourceReference, 2000) ||
            !IndependenceRecordValidation.ValidAttribution(ev.RequestId, item.Actor, item.RecordedAt, "firm_staff") ||
            item.Actor.Id != item.PartnerUserId.ToString() || _partnerEvaluations.Count >= 100 ||
            _partnerEvaluations.Any(existing => existing.EvaluationId == item.EvaluationId) ||
            Engagement(item.EngagementId) is not { Status: "draft" } engagement ||
            engagement.Revision != item.DraftRevision || !ValidRatifiedRules(ev.RatifiedRules) ||
            ev.RatifiedRules.Version != item.RuleVersion ||
            ev.RatifiedRules.RecordedAt > item.RecordedAt ||
            ev.RatifiedRules.Ratification?.RecordedAt > item.RecordedAt ||
            IndependenceSourceDigest.RuleContent(ev.RatifiedRules.Content) != item.RuleContentDigest ||
            IndependenceSourceDigest.Services(_services) != item.ServiceHistoryDigest ||
            !SameServiceSnapshots(item.CompleteServiceHistory) ||
            !MatchingManagementAcknowledgement(engagement, item.PartnerUserId, item.RecordedAt) ||
            !MatchesConditionalEvaluation(engagement, ev.RatifiedRules, item.ConsideredServiceRecordIds))
            throw new InvalidOperationException("A partner evaluation must preserve current exact client facts, authority and ratified rule sources.");

        Fence(_tenantId, ev.ExpectedSequence);
        var frozen = item with
        {
            CompleteServiceHistory = Array.AsReadOnly(item.CompleteServiceHistory
                .Select(IndependenceRecordValidation.Freeze).ToArray()),
            ConsideredServiceRecordIds = Array.AsReadOnly(item.ConsideredServiceRecordIds.ToArray())
        };
        _partnerEvaluations.Add(frozen);
        Remember(ev.RequestId, new RecordPartnerIndependenceEvaluation(_tenantId, frozen.EngagementId,
            frozen.EvaluationId, ev.ExpectedSequence, frozen.DraftRevision, frozen.RuleVersion,
            frozen.RuleContentDigest, frozen.ServiceHistoryDigest, frozen.Rationale), frozen.Actor, frozen);
    }

    bool MatchingManagementAcknowledgement(ServiceEngagementView engagement, Uuid partnerUserId,
        DateTimeOffset evaluatedAt) => _managementAcknowledgements.Any(ack =>
        ack.EngagementId == engagement.EngagementId && ack.EngagementRevision == engagement.Revision &&
        ack.UserId != partnerUserId && ack.RecordedAt <= evaluatedAt &&
        CompleteFacts(ack.CompleteServiceRecordIds));

    bool MatchesConditionalEvaluation(ServiceEngagementView engagement, IndependenceRuleVersionView rules,
        IReadOnlyList<Uuid> consideredIds)
    {
        var sources = ServiceRecords();
        var result = IndependenceCompartments.CanAcceptAttestEngagement(_tenantId, RuleSet(rules), sources,
            engagement.Content.PeriodStart, partnerEvaluationRecorded: false);
        var considered = result.ConsideredServices.Select(item => item.Service).ToHashSet();
        var expected = _services.Where((_, index) => considered.Contains(sources[index]))
            .Select(item => item.ServiceRecordId);
        return result.Code == IndependenceDecisionCode.PartnerEvaluationRequired &&
            result.EvaluationOutcome == IndependenceEvaluationOutcome.ConditionallyCompatible &&
            consideredIds is not null && consideredIds.SequenceEqual(expected);
    }

    NonattestServiceRecord[] ServiceRecords() => _services.Select(service => new NonattestServiceRecord(_tenantId,
        service.Content.ServiceEngagementId, service.Content.ServiceType, service.Content.StartedOn,
        service.Content.EndedOn, service.Content.FirmStaffMemberIds,
        service.Content.InvolvedManagementFunctions)).ToArray();

    static IndependenceRuleSet RuleSet(IndependenceRuleVersionView rules) => new(rules.Version,
        rules.Content.LookBackMonths, rules.Content.ServiceRules.Select(rule => new IndependenceServiceRule(
            rule.ServiceType, Classification(rule.Classification), Classification(rule.ManagementFunctionsClassification))).ToArray());

    static bool ValidRatifiedRules(IndependenceRuleVersionView rules) =>
        rules is { IsRatified: true, Version: > 0, Ratification: { } ratification } &&
        IndependenceRecordValidation.ValidRules(rules.Content) && rules.RecordedAt != default &&
        ratification.RuleVersion == rules.Version && ratification.RatificationId != Uuid.Empty &&
        ratification.RuleContentDigest == IndependenceSourceDigest.RuleContent(rules.Content) &&
        ratification.StaffMemberId != Uuid.Empty && ratification.UserId != Uuid.Empty &&
        ratification.DirectoryStaffRevision > 0 && ratification.DutyDesignationId != Uuid.Empty &&
        ratification.DutyRevision > 0 && Bounded(ratification.SourceReference, 2000) &&
        ratification.Actor is { Kind: "firm_staff" } && ratification.Actor.Id == ratification.UserId.ToString() &&
        ratification.RecordedAt != default;

    static bool ValidDigest(string value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    static bool Bounded(string value, int maximum) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximum && !value.Any(char.IsControl) && value == value.Trim();
}
