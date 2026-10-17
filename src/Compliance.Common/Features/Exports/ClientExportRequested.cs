using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports;

/// <summary>Records the ClientExportRequested domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.exports.client-export-requested", 1)]
public sealed record ClientExportRequested(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
