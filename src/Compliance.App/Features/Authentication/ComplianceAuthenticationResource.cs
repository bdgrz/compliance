namespace Bdgrz.Compliance.Features.Authentication;

/// <summary>A trusted resource-server token issuer and audience pair.</summary>
public sealed record ComplianceAuthenticationResource(
    string Name,
    string Authority,
    string Audience,
    bool RequireHttpsMetadata);
