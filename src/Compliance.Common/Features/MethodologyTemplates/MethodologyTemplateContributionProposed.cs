using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the MethodologyTemplateContributionProposed domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-contribution-proposed", 1)]
public sealed record MethodologyTemplateContributionProposed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
