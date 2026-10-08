using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Metadata only; rejected uploads expose a tombstone with no captured content metadata.</summary>
public sealed record EvidenceArtifactMetadataView(Uuid TenantId, Uuid ArtifactId, ulong SourcePosition,
    string ContentSha256, long ContentLength, EvidenceArtifactContent? Content,
    ActorReference Collector, DateTimeOffset RegisteredAt, string State, string? StateReason);
