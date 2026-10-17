using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports;

/// <summary>Records the ClientExportBuildFailed domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.exports.client-export-build-failed", 1)]
public sealed record ClientExportBuildFailed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
