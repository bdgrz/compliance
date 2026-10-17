using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Records the ProgramRolloverProposed domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.programs.program-rollover-proposed", 1)]
public sealed record ProgramRolloverProposed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
