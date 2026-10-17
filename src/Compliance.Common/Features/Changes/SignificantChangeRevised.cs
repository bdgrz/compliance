using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Records the SignificantChangeRevised domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.changes.significant-change-revised", 1)]
public sealed record SignificantChangeRevised(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
