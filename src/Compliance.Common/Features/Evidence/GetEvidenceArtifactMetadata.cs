using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

[Discriminator("bdgrz.evidence.artifact.metadata.get", 1)]
public sealed record GetEvidenceArtifactMetadata(Uuid TenantId, Uuid ArtifactId)
    : IRequest<EvidenceArtifactMetadataView>, ICallable;
