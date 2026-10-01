namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Derived access of one review subject to one entitlement with every path that conveys it.
///     It is calculated from the observed facts and never stored as a source fact.
///     <c>ExpiresAt</c> is null when any path has no expiry.
/// </summary>
public sealed record EffectiveAccessView(string ProviderSubjectId, string ProviderEntitlementId,
    bool Direct, IReadOnlyList<EffectiveAccessPathView> Paths, DateTimeOffset? ExpiresAt);
