using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

[Discriminator("bdgrz.artifact_retention.get", 1)]
public sealed record GetArtifactRetention(Uuid TenantId, string SourceKind, Uuid SourceId, long? MinimumRevision = null)
    : IRequest<ArtifactRetentionView>, IArtifactRetentionAdminRequest, ICallable;
