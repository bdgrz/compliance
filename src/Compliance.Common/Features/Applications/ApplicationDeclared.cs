using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.declared", 1)]
public sealed record ApplicationDeclared(Uuid TenantId, Uuid ApplicationId,
    string Name, string Purpose, string? OwnerReference,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt,
    string? Classification = null) : DomainEvent;
