using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.revised", 1)]
public sealed record ApplicationRevised(Uuid TenantId, Uuid ApplicationId, long Revision,
    string Name, string Purpose, string? OwnerReference,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt,
    string? Classification = null) : DomainEvent;
