using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Records the PartnerIndependenceEvaluationRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.accesscontrol.partner-independence-evaluation-recorded", 1)]
public sealed record PartnerIndependenceEvaluationRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
