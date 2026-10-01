namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     One hop of an effective-access path: <c>group_member</c> or <c>role_assumption</c> from a
///     member to its container, or the final <c>access_assignment</c> to an entitlement.
/// </summary>
public sealed record EffectiveAccessHop(string Kind, string From, string To);
