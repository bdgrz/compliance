using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Records the SystemDescriptionNoChangeRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.systemdescription.system-description-no-change-recorded", 1)]
public sealed record SystemDescriptionNoChangeRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
