using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Admin-only import consumer of canonical retention authority.</summary>
[Discriminator("bdgrz.application_import.retention.get", 1)]
public sealed record GetApplicationImportRetention(Uuid TenantId, Uuid BatchId, long? MinimumRevision = null)
    : IRequest<ArtifactRetentionView>, IArtifactRetentionAdminRequest, ICallable;
