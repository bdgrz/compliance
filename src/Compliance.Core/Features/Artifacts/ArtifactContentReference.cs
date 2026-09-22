using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Artifacts;

/// <summary>
/// Identifies immutable artifact content inside exactly one tenant by its lowercase SHA-256 digest and length.
/// </summary>
public sealed record ArtifactContentReference(Uuid TenantId, string Sha256, long Length);
