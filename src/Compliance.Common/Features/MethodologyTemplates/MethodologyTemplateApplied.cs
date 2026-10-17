using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the MethodologyTemplateApplied domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-applied", 1)]
public sealed record MethodologyTemplateApplied(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
