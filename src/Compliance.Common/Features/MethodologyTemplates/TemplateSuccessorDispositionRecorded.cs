using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the TemplateSuccessorDispositionRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.methodologytemplates.template-successor-disposition-recorded", 1)]
public sealed record TemplateSuccessorDispositionRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
