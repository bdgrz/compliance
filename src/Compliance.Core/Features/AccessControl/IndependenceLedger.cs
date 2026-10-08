using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Serializes client-owned service facts and immutable, version-bound independence previews.</summary>
public sealed class IndependenceLedger : Aggregate
{
    readonly Uuid _tenantId;
    readonly List<NonattestServiceView> _services = [];
    readonly List<IndependenceEvaluationView> _evaluations = [];
    readonly Dictionary<Uuid, (string Intent, ActorReference Actor, object Response)> _decisions = [];

    public IndependenceLedger(Uuid tenantId) : base(tenantId,
        new EventStreamAddress(tenantId.ToString(), "client-independence", tenantId.ToString()))
    {
        _tenantId = tenantId;
        On<NonattestServiceRecorded>(Apply);
        On<ClientIndependenceEvaluated>(Apply);
    }

    public long Sequence { get; private set; }

    public Result<NonattestServiceView> RecordService(Uuid requestId, Uuid serviceRecordId, long expectedSequence, NonattestServiceContent content,
        ActorReference actor, DateTimeOffset recordedAt)
    {
        if (_tenantId == Uuid.Empty || !IndependenceRecordValidation.ValidAttribution(requestId, actor, recordedAt, "member") || serviceRecordId == Uuid.Empty || !IndependenceRecordValidation.ValidService(content))
            return Failure<NonattestServiceView>(RequestErrorKind.Validation,
                "A service requires an owning client, immutable identity, valid period, staff and attributed source.");
        content = content with { FirmStaffMemberIds = Array.AsReadOnly(content.FirmStaffMemberIds.ToArray()) };
        var request = new RecordNonattestService(_tenantId, serviceRecordId, expectedSequence, content);
        if (Retry<NonattestServiceView>(requestId, request, actor) is { } retry)
            return retry;
        if (expectedSequence != Sequence || _services.Any(service => service.ServiceRecordId == serviceRecordId) ||
            _services.Count >= 100)
            return Failure<NonattestServiceView>(RequestErrorKind.Conflict,
                "Reload the sequence; service identities are immutable and complete client histories are bounded to one hundred records.");
        var service = new NonattestServiceView(_tenantId, serviceRecordId, content, actor, recordedAt);
        var recorded = new NonattestServiceRecorded(_tenantId, requestId, expectedSequence, service);
        if (!Fits(recorded))
            return Failure<NonattestServiceView>(RequestErrorKind.Validation, "The service event exceeds the bounded payload.");
        RaiseEvent(recorded);
        return Result<NonattestServiceView>.Success(service);
    }

    public Result<IndependenceEvaluationView> Evaluate(Uuid requestId, Uuid evaluationId, long expectedSequence, IndependenceRuleVersionView version, DateOnly periodStart,
        ActorReference actor, DateTimeOffset recordedAt)
    {
        if (_tenantId == Uuid.Empty || !IndependenceRecordValidation.ValidAttribution(requestId, actor, recordedAt, "member") || evaluationId == Uuid.Empty || periodStart == DateOnly.MinValue || version is null || version.IsRatified ||
            version.Version <= 0 || !IndependenceRecordValidation.ValidRules(version.Content))
            return Failure<IndependenceEvaluationView>(RequestErrorKind.Validation,
                "An evaluation requires an owning client, immutable identity, examination period and member attribution.");
        version = IndependenceRecordValidation.Freeze(version);
        var request = new EvaluateClientIndependence(_tenantId, evaluationId, expectedSequence, version.Version, periodStart);
        if (Retry<IndependenceEvaluationView>(requestId, request, actor) is { } retry)
            return retry;
        if (expectedSequence != Sequence ||
            _evaluations.Any(evaluation => evaluation.EvaluationId == evaluationId) || _evaluations.Count >= 100)
            return Failure<IndependenceEvaluationView>(RequestErrorKind.Conflict,
                "Reload the sequence; evaluations require authored rules and a bounded immutable identity.");
        var history = _services.ToArray();
        var source = history.Select(service => new NonattestServiceRecord(_tenantId,
            service.Content.ServiceEngagementId, service.Content.ServiceType, service.Content.StartedOn,
            service.Content.EndedOn, service.Content.FirmStaffMemberIds, service.Content.InvolvedManagementFunctions)).ToArray();
        var policy = new IndependenceRuleSet(version.Version, version.Content.LookBackMonths,
            version.Content.ServiceRules.Select(rule => new IndependenceServiceRule(rule.ServiceType,
                Classification(rule.Classification), Classification(rule.ManagementFunctionsClassification))).ToArray());
        var decision = IndependenceCompartments.CanAcceptAttestEngagement(_tenantId, policy,
            source, periodStart, partnerEvaluationRecorded: false);
        if (decision.Code is IndependenceDecisionCode.EvaluationInputInvalid or IndependenceDecisionCode.RuleSetInvalid or
            IndependenceDecisionCode.ServiceHistoryInvalid)
            return Failure<IndependenceEvaluationView>(RequestErrorKind.Validation,
                "The examination date cannot be evaluated against the retained rule and service history.");
        var considered = decision.ConsideredServices.Select(assessment => assessment.Service).ToHashSet();
        var evaluation = new IndependenceEvaluationView(evaluationId, _tenantId, version.Version, version,
            periodStart, WireCode(decision.Code), decision.EvaluationOutcome switch
            {
                IndependenceEvaluationOutcome.Compatible => "compatible",
                IndependenceEvaluationOutcome.ConditionallyCompatible => "conditionally_compatible",
                IndependenceEvaluationOutcome.Impaired => "impaired",
                _ => null,
            }, Array.AsReadOnly(history), Array.AsReadOnly(history.Where((_, index) => considered.Contains(source[index]))
                .Select(service => service.ServiceRecordId).ToArray()), actor, recordedAt, true);
        var evaluated = new ClientIndependenceEvaluated(_tenantId, requestId, expectedSequence, evaluation);
        if (!Fits(evaluated))
            return Failure<IndependenceEvaluationView>(RequestErrorKind.Validation,
                "The complete retained evaluation exceeds the bounded event payload; history was not truncated.");
        RaiseEvent(evaluated);
        return Result<IndependenceEvaluationView>.Success(evaluation);
    }

    public IndependenceHistoryView History() => new(_tenantId, Sequence,
        Array.AsReadOnly(_evaluations.Select(evaluation => evaluation.EvaluatedRules)
            .DistinctBy(version => version.Version).OrderBy(version => version.Version).ToArray()),
        Array.AsReadOnly(_services.ToArray()), Array.AsReadOnly(_evaluations.ToArray()));

    void Apply(NonattestServiceRecorded recorded)
    {
        Fence(recorded.TenantId, recorded.ExpectedSequence);
        if (recorded.Service.ClientTenantId != _tenantId ||
            _services.Any(service => service.ServiceRecordId == recorded.Service.ServiceRecordId))
            throw new InvalidOperationException("Service identities cannot be rewritten.");
        var service = IndependenceRecordValidation.Freeze(recorded.Service);
        _services.Add(service);
        Remember(recorded.RequestId, new RecordNonattestService(_tenantId, service.ServiceRecordId,
            recorded.ExpectedSequence, service.Content), service.Actor, service);
    }

    void Apply(ClientIndependenceEvaluated evaluated)
    {
        Fence(evaluated.TenantId, evaluated.ExpectedSequence);
        if (evaluated.Evaluation.ClientTenantId != _tenantId || !evaluated.Evaluation.ProductionAcceptanceBlocked ||
            _evaluations.Any(evaluation => evaluation.EvaluationId == evaluated.Evaluation.EvaluationId))
            throw new InvalidOperationException("An evaluation preview cannot grant production acceptance or rewrite history.");
        var evaluation = IndependenceRecordValidation.Freeze(evaluated.Evaluation);
        _evaluations.Add(evaluation);
        Remember(evaluated.RequestId, new EvaluateClientIndependence(_tenantId, evaluation.EvaluationId,
            evaluated.ExpectedSequence, evaluation.RuleSetVersion, evaluation.ExaminationPeriodStart),
            evaluation.Actor, evaluation);
    }

    void Fence(Uuid tenantId, long expectedSequence)
    {
        if (tenantId != _tenantId || expectedSequence != Sequence)
            throw new InvalidOperationException("An independence event must match its owning tenant and sequence.");
        Sequence++;
    }

    void Remember<T>(Uuid requestId, T request, ActorReference actor, object response) =>
        _decisions.Add(requestId, (Intent(request), actor, response));

    Result<T>? Retry<T>(Uuid requestId, object request, ActorReference actor)
    {
        if (!_decisions.TryGetValue(requestId, out var previous))
            return null;
        return previous.Intent == Intent(request) && previous.Actor == actor && previous.Response is T response
            ? Result<T>.Success(response)
            : Failure<T>(RequestErrorKind.Conflict, "The request identity already retains a different independence intent or actor.");
    }

    static string Intent<T>(T value) => JsonSerializer.Serialize(value, value!.GetType(), ComplianceCoreJsonContext.Default);
    static bool Fits<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value,
        value!.GetType(), ComplianceCoreJsonContext.Default).Length <= 48 * 1024;

    static IndependenceServiceClassification Classification(string value) => value switch
    {
        "compatible" => IndependenceServiceClassification.Compatible,
        "conditionally_compatible" => IndependenceServiceClassification.ConditionallyCompatible,
        "impairing" => IndependenceServiceClassification.Impairing,
        _ => throw new InvalidOperationException("An authored classification was invalid."),
    };
    static string WireCode(IndependenceDecisionCode code) => code switch
    {
        IndependenceDecisionCode.Allowed => "allowed",
        IndependenceDecisionCode.RecentImpairingService => "recent_impairing_service",
        IndependenceDecisionCode.PartnerEvaluationRequired => "partner_evaluation_required",
        IndependenceDecisionCode.ServiceNotClassified => "service_not_classified",
        _ => throw new InvalidOperationException("An invalid policy result cannot be retained as an evaluation."),
    };
    static Result<T> Failure<T>(RequestErrorKind kind, string message) =>
        Result<T>.Failure(new RequestError(kind, message));
}
