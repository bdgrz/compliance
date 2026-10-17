using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the MethodologyTemplateContributionAccepted domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-contribution-accepted", 1)]
public sealed record MethodologyTemplateContributionAccepted(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
