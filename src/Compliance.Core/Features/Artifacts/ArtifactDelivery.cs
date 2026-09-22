namespace Bdgrz.Compliance.Features.Artifacts;

/// <summary>
/// An opaque, short-lived delivery location for verified content. Callers must not parse the location.
/// </summary>
public sealed record ArtifactDelivery(ArtifactContentReference Content, Uri Location, DateTimeOffset ExpiresAt);
