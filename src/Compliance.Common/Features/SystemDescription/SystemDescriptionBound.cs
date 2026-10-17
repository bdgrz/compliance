using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Records the SystemDescriptionBound domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.systemdescription.system-description-bound", 1)]
public sealed record SystemDescriptionBound(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
