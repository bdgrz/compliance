using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the AdvisorHandoffRevoked domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.advisorcollaboration.advisor-handoff-revoked", 1)]
public sealed record AdvisorHandoffRevoked(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
