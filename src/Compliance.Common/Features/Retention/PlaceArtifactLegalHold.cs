using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

[Discriminator("bdgrz.artifact_retention.legal_hold.place", 1)]
public sealed record PlaceArtifactLegalHold(Uuid TenantId, string SourceKind, Uuid SourceId, string ExpectedContentSha256,
    Uuid HoldId, long ExpectedRevision, string Reason) : IRequest, IArtifactRetentionAdminRequest, IClientManagementMutationRequest, ICallable;
