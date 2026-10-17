using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the ArtifactDisclosureDelivered domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.retention.artifact-disclosure-delivered", 1)]
public sealed record ArtifactDisclosureDelivered(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
