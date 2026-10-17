using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

/// <summary>Records the CommitmentRetired domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.commitments.commitment-retired", 1)]
public sealed record CommitmentRetired(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
