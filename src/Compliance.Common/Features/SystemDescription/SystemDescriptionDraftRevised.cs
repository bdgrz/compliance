using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Records the SystemDescriptionDraftRevised domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.systemdescription.system-description-draft-revised", 1)]
public sealed record SystemDescriptionDraftRevised(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
