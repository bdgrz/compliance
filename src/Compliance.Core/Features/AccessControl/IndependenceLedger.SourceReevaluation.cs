using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed partial class IndependenceLedger
{
    const int MaximumSourceReevaluations = 1000;
    const int MaximumSourceTransactionBytes = 48 * 1024;
    readonly List<ServiceIndependenceReevaluationView> _sourceReevaluations = [];
    readonly Dictionary<Uuid, long> _serviceSourceSequences = [];

    Result<IReadOnlyList<ServiceIndependenceReevaluated>> PrepareSourceReevaluations(NonattestServiceRecorded cause)
    {
        var affected = _acceptances.Values.Where(acceptance => acceptance.Status == "active" &&
                Engagement(acceptance.EngagementId)?.Content.Practice == "attest")
            .OrderBy(acceptance => acceptance.EngagementId.ToString(), StringComparer.Ordinal).ToArray();
        if (_sourceReevaluations.Count + affected.Length > MaximumSourceReevaluations)
            return Failure<IReadOnlyList<ServiceIndependenceReevaluated>>(RequestErrorKind.Conflict,
                "Complete retained source reevaluation history exceeds its bound; no service was recorded.");
        var history = _services.Append(cause.Service).Select(IndependenceRecordValidation.Freeze).ToArray();
        var events = new List<ServiceIndependenceReevaluated>();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(cause, ComplianceCoreJsonContext.Default.NonattestServiceRecorded).Length;
        foreach (var acceptance in affected)
        {
            if (!ValidReevaluationChronology(cause.Service, acceptance))
                return Failure<IReadOnlyList<ServiceIndependenceReevaluated>>(RequestErrorKind.Validation,
                    "A service change cannot precede its retained accepted source or later service facts.");
            var receipt = SourceReevaluation(cause.RequestId, cause.ExpectedSequence + 1, cause.Service,
                acceptance, history);
            var ev = new ServiceIndependenceReevaluated(_tenantId, cause.ExpectedSequence + 1 + events.Count, receipt);
            bytes += JsonSerializer.SerializeToUtf8Bytes(ev, ComplianceCoreJsonContext.Default.ServiceIndependenceReevaluated).Length;
            if (bytes > MaximumSourceTransactionBytes)
                return Failure<IReadOnlyList<ServiceIndependenceReevaluated>>(RequestErrorKind.Validation,
                    "The complete cause and source reevaluation transaction exceeds its payload bound; no service was recorded.");
            events.Add(ev);
        }
        return Result<IReadOnlyList<ServiceIndependenceReevaluated>>.Success(Array.AsReadOnly(events.ToArray()));
    }

    bool ValidReevaluationChronology(NonattestServiceView cause, ServiceEngagementAcceptanceView acceptance) =>
        cause.RecordedAt >= (acceptance.ChangedAt ?? acceptance.RecordedAt) &&
        _services.All(service => service.RecordedAt <= cause.RecordedAt);

    ServiceIndependenceReevaluationView SourceReevaluation(Uuid causalRequestId, long sourceSequence,
        NonattestServiceView cause, ServiceEngagementAcceptanceView acceptance,
        IReadOnlyList<NonattestServiceView> history)
    {
        var engagement = Engagement(acceptance.EngagementId)!;
        var rules = IndependenceRecordValidation.Freeze(acceptance.Rules);
        var policy = new IndependenceRuleSet(rules.Version, rules.Content.LookBackMonths,
            rules.Content.ServiceRules.Select(rule => new IndependenceServiceRule(rule.ServiceType,
                Classification(rule.Classification), Classification(rule.ManagementFunctionsClassification))).ToArray());
        var facts = history.Select(service => new NonattestServiceRecord(_tenantId,
            service.Content.ServiceEngagementId, service.Content.ServiceType, service.Content.StartedOn,
            service.Content.EndedOn, service.Content.FirmStaffMemberIds, service.Content.InvolvedManagementFunctions)).ToArray();
        // Earlier partner evidence belongs only to the frozen acceptance, never to these changed facts.
        var decision = IndependenceCompartments.CanAcceptAttestEngagement(_tenantId, policy, facts,
            engagement.Content.PeriodStart, partnerEvaluationRecorded: false);
        if (decision.Code is not (IndependenceDecisionCode.Allowed or IndependenceDecisionCode.RecentImpairingService or
                IndependenceDecisionCode.PartnerEvaluationRequired or IndependenceDecisionCode.ServiceNotClassified))
            throw new InvalidOperationException("A retained accepted source cannot produce an invalid reevaluation policy input.");
        // The frozen policy evaluates the original period-start look-back. New during-period
        // facts require a fresh judgment that this denial-only source receipt cannot supply.
        var acceptedServiceIds = acceptance.CompleteServiceHistory.Select(service => service.ServiceRecordId).ToHashSet();
        var duringPeriod = history.Any(service => !acceptedServiceIds.Contains(service.ServiceRecordId) &&
            service.Content.StartedOn > engagement.Content.PeriodStart &&
            (engagement.Content.PeriodEnd is null || service.Content.StartedOn <= engagement.Content.PeriodEnd));
        var knownImpairment = decision.ConsideredServices.Any(assessment =>
            assessment.Classification == IndependenceServiceClassification.Impairing);
        var code = knownImpairment ? "recent_impairing_service" :
            decision.Code == IndependenceDecisionCode.Allowed && duringPeriod
                ? "during_period_service_requires_review" : WireCode(decision.Code);
        var state = knownImpairment ? "impaired" :
            decision.Code == IndependenceDecisionCode.Allowed && !duringPeriod ? "compatible" : "review_required";
        var considered = decision.ConsideredServices.Select(item => item.Service).ToHashSet();
        var digest = AcceptanceDigest(acceptance);
        return new ServiceIndependenceReevaluationView(
            Uuid.CreateVersion5(causalRequestId, $"service_reevaluation_v1:{acceptance.EngagementId}:{acceptance.Revision}:{digest}"),
            _tenantId, causalRequestId, cause.ServiceRecordId, sourceSequence, acceptance.EngagementId,
            acceptance.Revision, digest, rules, engagement.Content.PeriodStart, engagement.Content.PeriodEnd,
            engagement.Content.PeriodStart, WireCode(decision.Code), duringPeriod,
            Array.AsReadOnly(history.Select(IndependenceRecordValidation.Freeze).ToArray()),
            Array.AsReadOnly(history.Where((_, index) => considered.Contains(facts[index])).Select(service => service.ServiceRecordId).ToArray()),
            code, state,
            true, cause.Actor, cause.RecordedAt);
    }

    static string AcceptanceDigest(ServiceEngagementAcceptanceView acceptance) =>
        IndependenceSourceDigest.Acceptance(acceptance);

    static ServiceIndependenceReevaluationView FreezeReevaluation(ServiceIndependenceReevaluationView receipt) => receipt with
    {
        AcceptedRules = IndependenceRecordValidation.Freeze(receipt.AcceptedRules),
        CompleteServiceHistory = Array.AsReadOnly(receipt.CompleteServiceHistory.Select(IndependenceRecordValidation.Freeze).ToArray()),
        ConsideredServiceRecordIds = Array.AsReadOnly(receipt.ConsideredServiceRecordIds.ToArray())
    };

    void Apply(ServiceIndependenceReevaluated ev)
    {
        var receipt = ev.Reevaluation;
        if (receipt is null || ev.TenantId != _tenantId || receipt.TenantId != _tenantId || ev.ExpectedSequence != Sequence ||
            _sourceReevaluations.Count >= MaximumSourceReevaluations || _sourceReevaluations.Any(item => item.ReevaluationId == receipt.ReevaluationId) ||
            !_decisions.TryGetValue(receipt.CausalRequestId, out var cause) || cause.Response is not NonattestServiceView service ||
            service.ServiceRecordId != receipt.ServiceRecordId ||
            !IndependenceRecordValidation.ValidAttribution(receipt.CausalRequestId, service.Actor, service.RecordedAt, "member") ||
            !IndependenceRecordValidation.ValidService(service.Content) ||
            !_serviceSourceSequences.TryGetValue(service.ServiceRecordId, out var sourceSequence) || sourceSequence != receipt.SourceSequence ||
            Sequence != sourceSequence + _sourceReevaluations.Count(item => item.CausalRequestId == receipt.CausalRequestId) ||
            Acceptance(receipt.EngagementId) is not { Status: "active" } acceptance ||
            Engagement(receipt.EngagementId)?.Content.Practice != "attest" || !ValidReevaluationChronology(service, acceptance) ||
            Intent(receipt) != Intent(SourceReevaluation(receipt.CausalRequestId, sourceSequence, service, acceptance, _services)))
            throw new InvalidOperationException("A source reevaluation must preserve its exact tenant, cause, accepted snapshot, chronology and complete facts.");
        var frozen = FreezeReevaluation(receipt);
        Fence(ev.TenantId, ev.ExpectedSequence);
        _sourceReevaluations.Add(frozen);
    }
}
