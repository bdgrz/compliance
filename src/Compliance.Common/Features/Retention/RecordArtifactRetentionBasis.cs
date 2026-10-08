using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

[Discriminator("bdgrz.artifact_retention.basis.record", 1)]
public sealed record RecordArtifactRetentionBasis(Uuid TenantId, string SourceKind, Uuid SourceId,
    string ExpectedContentSha256, long ExpectedRevision, string Reason, DateOnly? PeriodStart = null, DateOnly? PeriodEnd = null)
    : IRequest, IArtifactRetentionAdminRequest, IClientManagementMutationRequest, ICallable;
