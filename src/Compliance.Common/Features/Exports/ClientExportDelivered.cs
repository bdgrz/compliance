using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports;

/// <summary>Records the ClientExportDelivered domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.exports.client-export-delivered", 1)]
public sealed record ClientExportDelivered(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
