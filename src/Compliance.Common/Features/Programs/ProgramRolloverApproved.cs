using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Records the ProgramRolloverApproved domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.programs.program-rollover-approved", 1)]
public sealed record ProgramRolloverApproved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
