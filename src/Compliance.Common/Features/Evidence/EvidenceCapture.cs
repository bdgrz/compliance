using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     The outcome of capturing content as evidence. <see cref="Duplicate" /> is true when the tenant already holds
///     the same content. A pending artifact resumes inspection, and a recorded quarantine or rejection resumes its
///     required storage effect; the initial artifact metadata and collector remain unchanged.
/// </summary>
public sealed record EvidenceCapture(Uuid ArtifactId, string State, string? Reason, bool Duplicate);
