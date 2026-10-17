using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the MethodologyTemplateCreated domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.methodologytemplates.methodology-template-created", 1)]
public sealed record MethodologyTemplateCreated(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
