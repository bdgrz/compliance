using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Records the SystemDescriptionReviewed domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.systemdescription.system-description-reviewed", 1)]
public sealed record SystemDescriptionReviewed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
