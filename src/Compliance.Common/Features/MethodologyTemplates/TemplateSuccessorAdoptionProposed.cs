using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the TemplateSuccessorAdoptionProposed domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.methodologytemplates.template-successor-adoption-proposed", 1)]
public sealed record TemplateSuccessorAdoptionProposed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
