using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the RepresentationLetterRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.examinations.representation-letter-recorded", 1)]
public sealed record RepresentationLetterRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
