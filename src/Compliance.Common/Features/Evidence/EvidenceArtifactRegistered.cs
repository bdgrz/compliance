using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Registers uploaded content as evidence awaiting inspection; the content is not yet available.</summary>
[Discriminator("bdgrz.evidence.artifact.registered", 1)]
public sealed record EvidenceArtifactRegistered(Uuid TenantId, Uuid ArtifactId,
    EvidenceArtifactContent Content, string ContentSha256, long ContentLength,
    ActorReference Collector, DateTimeOffset RegisteredAt) : DomainEvent;
