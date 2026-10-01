namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     An observed provider principal. <c>PrincipalKind</c> is <c>user_account</c>, <c>group</c>,
///     <c>role</c>, <c>service_principal</c>, <c>workload_identity</c>, <c>access_token</c>, or
///     <c>app_installation</c>. Groups and roles are access structures, never review subjects.
/// </summary>
public sealed record AccessPrincipalFact(string ProviderSubjectId, string PrincipalKind,
    string DisplayName, string Status, string? Email = null);
