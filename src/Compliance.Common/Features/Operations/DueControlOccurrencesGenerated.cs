using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records the DueControlOccurrencesGenerated domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.operations.due-control-occurrences-generated", 1)]
public sealed record DueControlOccurrencesGenerated(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
