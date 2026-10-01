namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     An observed provider entitlement. <c>EntitlementKind</c> is <c>membership</c>,
///     <c>role_assignment</c>, <c>permission_set_assignment</c>, <c>permission</c>,
///     <c>repository_access</c>, or <c>admin_privilege</c>.
/// </summary>
public sealed record AccessEntitlementFact(string ProviderEntitlementId, string EntitlementKind,
    string DisplayName);
