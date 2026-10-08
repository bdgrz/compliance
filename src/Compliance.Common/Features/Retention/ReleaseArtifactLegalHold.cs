using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

[Discriminator("bdgrz.artifact_retention.legal_hold.release", 1)]
public sealed record ReleaseArtifactLegalHold(Uuid TenantId, string SourceKind, Uuid SourceId, string ExpectedContentSha256,
    Uuid HoldId, long ExpectedRevision, string Reason) : IRequest, IArtifactRetentionAdminRequest, ICallable;
