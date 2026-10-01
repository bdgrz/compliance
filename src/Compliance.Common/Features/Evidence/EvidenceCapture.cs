using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     The outcome of capturing content as evidence. <see cref="Duplicate" /> is true when the tenant already holds
///     the same content, in which case the existing artifact and its current state are returned unchanged.
/// </summary>
public sealed record EvidenceCapture(Uuid ArtifactId, string State, string? Reason, bool Duplicate);
