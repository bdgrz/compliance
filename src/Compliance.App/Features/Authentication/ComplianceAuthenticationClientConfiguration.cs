namespace Bdgrz.Compliance.Features.Authentication;

sealed record ComplianceAuthenticationClientConfiguration(
    bool Enabled,
    bool DeveloperIdentityEnabled,
    string? Issuer,
    string? ClientId,
    string[] Scopes,
    string? AuthorizationAudience)
{
    internal static ComplianceAuthenticationClientConfiguration Development { get; } =
        new(false, true, null, null, [], null);
}
