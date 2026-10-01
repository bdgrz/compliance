namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Public canonical person fields observed in an HRIS or identity-provider source.</summary>
public sealed record WorkforcePersonSourceFacts(string DisplayName, string? WorkEmail);
