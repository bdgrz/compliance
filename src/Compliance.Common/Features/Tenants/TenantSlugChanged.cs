using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-slug.changed", 1)]
public sealed record TenantSlugChanged(Uuid TenantId, string OldSlug, string NewSlug) : DomainEvent
{
    public ActorReference? RequestedBy { get; init; }
    public DateTimeOffset? RequestedAt { get; init; }
}
