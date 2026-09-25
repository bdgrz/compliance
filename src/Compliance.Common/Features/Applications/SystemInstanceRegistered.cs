using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.system_instance.registered", 1)]
public sealed record SystemInstanceRegistered(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long Revision, string Name, string Kind,
    string? AccessBoundaryReference, string? SourceIdentifier,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent;
