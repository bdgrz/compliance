using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Records the SystemDescriptionSuccessorProposed domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.systemdescription.system-description-successor-proposed", 1)]
public sealed record SystemDescriptionSuccessorProposed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
