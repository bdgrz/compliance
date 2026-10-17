using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Records the IndependenceRuleVersionRatified domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.accesscontrol.independence-rule-version-ratified", 1)]
public sealed record IndependenceRuleVersionRatified(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
