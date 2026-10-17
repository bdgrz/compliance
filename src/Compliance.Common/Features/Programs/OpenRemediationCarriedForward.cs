using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Records the OpenRemediationCarriedForward domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.programs.open-remediation-carried-forward", 1)]
public sealed record OpenRemediationCarriedForward(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
