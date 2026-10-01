namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>An ordered path from a review subject to an entitlement; <c>ExpiresAt</c> is the assignment expiry.</summary>
public sealed record EffectiveAccessPathView(IReadOnlyList<EffectiveAccessHop> Hops,
    DateTimeOffset? ExpiresAt);
