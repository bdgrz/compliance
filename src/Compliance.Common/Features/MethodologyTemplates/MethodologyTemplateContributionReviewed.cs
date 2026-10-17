using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the MethodologyTemplateContributionReviewed domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-contribution-reviewed", 1)]
public sealed record MethodologyTemplateContributionReviewed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
