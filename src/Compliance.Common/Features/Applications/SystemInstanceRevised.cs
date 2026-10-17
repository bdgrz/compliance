using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Records the SystemInstanceRevised domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.applications.system-instance-revised", 1)]
public sealed record SystemInstanceRevised(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
