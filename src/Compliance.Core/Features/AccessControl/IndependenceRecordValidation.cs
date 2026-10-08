using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

static class IndependenceRecordValidation
{
    internal static bool ValidAttribution(Uuid requestId, ActorReference actor, DateTimeOffset recordedAt, string kind) =>
        requestId != Uuid.Empty && actor is not null && actor.Kind == kind &&
        Uuid.TryParse(actor.Id, out var memberId) && memberId != Uuid.Empty && Bounded(actor.Display, 200) &&
        recordedAt != default;

    internal static bool ValidRules(IndependenceRuleContent content) => content is not null &&
        content.LookBackMonths is >= 12 and <= 1200 && Bounded(content.SourceReference, 2000) &&
        content.ServiceRules is { Count: > 0 and <= 100 } && content.ServiceRules.All(rule =>
            rule is not null && Bounded(rule.ServiceType, 100) && ValidClassification(rule.Classification) &&
            ValidClassification(rule.ManagementFunctionsClassification) && rule.ManagementFunctionsClassification == "impairing") &&
        content.ServiceRules.Select(rule => rule.ServiceType).Distinct(StringComparer.Ordinal).Count() == content.ServiceRules.Count;

    internal static bool ValidService(NonattestServiceContent content) => content is not null &&
        content.ServiceEngagementId != Uuid.Empty && Bounded(content.ServiceType, 100) &&
        content.StartedOn != DateOnly.MinValue && (content.EndedOn is null || content.EndedOn >= content.StartedOn) &&
        content.FirmStaffMemberIds is { Count: > 0 and <= 100 } && !content.FirmStaffMemberIds.Contains(Uuid.Empty) &&
        content.FirmStaffMemberIds.Distinct().Count() == content.FirmStaffMemberIds.Count && Bounded(content.SourceReference, 2000);

    internal static IndependenceRuleVersionView Freeze(IndependenceRuleVersionView version) => version with
    {
        Content = version.Content with { ServiceRules = Array.AsReadOnly(version.Content.ServiceRules.ToArray()) }
    };

    internal static NonattestServiceView Freeze(NonattestServiceView service) => service with
    {
        Content = service.Content with { FirmStaffMemberIds = Array.AsReadOnly(service.Content.FirmStaffMemberIds.ToArray()) }
    };

    internal static IndependenceEvaluationView Freeze(IndependenceEvaluationView evaluation) => evaluation with
    {
        EvaluatedRules = Freeze(evaluation.EvaluatedRules),
        CompleteServiceHistory = Array.AsReadOnly(evaluation.CompleteServiceHistory.Select(Freeze).ToArray()),
        ConsideredServiceRecordIds = Array.AsReadOnly(evaluation.ConsideredServiceRecordIds.ToArray())
    };

    static bool Bounded(string value, int maximum) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximum && !value.Any(char.IsControl) && value == value.Trim();
    static bool ValidClassification(string value) => value is "compatible" or "conditionally_compatible" or "impairing";
}
