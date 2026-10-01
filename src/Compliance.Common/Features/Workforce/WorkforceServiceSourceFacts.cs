namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Provider observations corroborate identity facts; they never govern NHI owner or purpose.</summary>
public sealed record WorkforceServiceSourceFacts(string DisplayName, string IdentityKind,
    string? Environment, string LifecycleStatus, DateOnly? ExpiresOn);
