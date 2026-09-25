using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.system_instance.declared", 1)]
public sealed record SystemInstanceDeclared(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long ApplicationRevision, string Name, string Kind,
    string? AccessBoundaryReference, string? SourceIdentifier,
    Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ChangedAt) : DomainEvent;
