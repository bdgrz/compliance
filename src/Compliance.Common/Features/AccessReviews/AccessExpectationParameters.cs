namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     What an expectation matches. <c>forbidden_principal_kind</c> uses <c>PrincipalKind</c>;
///     <c>privileged_entitlement</c> and <c>requires_expiry</c> use <c>EntitlementKind</c> or
///     <c>ProviderEntitlementId</c>, optionally narrowed by <c>PrincipalKind</c>;
///     <c>required_access</c> uses <c>ProviderSubjectId</c> and <c>ProviderEntitlementId</c>.
/// </summary>
public sealed record AccessExpectationParameters(string? PrincipalKind = null,
    string? EntitlementKind = null, string? ProviderEntitlementId = null,
    string? ProviderSubjectId = null);
