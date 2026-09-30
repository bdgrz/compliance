namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Evaluates an ordered, effective-dated scope history at one instant.</summary>
public static class AccessReviewScopeStatus
{
    public const string Unresolved = "unresolved";

    public static AccessReviewScopeDecisionView? EffectiveAt(
        IReadOnlyList<AccessReviewScopeDecisionView> decisions, DateTimeOffset asOf) =>
        decisions.LastOrDefault(decision => decision.EffectiveFrom <= asOf);

    public static AccessReviewScopeDecisionView? UpcomingAfter(
        IReadOnlyList<AccessReviewScopeDecisionView> decisions, DateTimeOffset asOf) =>
        decisions.FirstOrDefault(decision => decision.EffectiveFrom > asOf);

    public static AccessReviewScopeStatusView Evaluate(SystemInstanceView instance,
        IReadOnlyList<AccessReviewScopeDecisionView> decisions, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(decisions);
        var effective = EffectiveAt(decisions, asOf);
        return new AccessReviewScopeStatusView(instance.TenantId, instance.ApplicationId,
            instance.SystemInstanceId, instance.Lifecycle, asOf,
            effective?.Decision ?? Unresolved, effective?.ReviewBy is { } reviewBy && reviewBy <= asOf,
            decisions.Count, effective, UpcomingAfter(decisions, asOf));
    }
}
