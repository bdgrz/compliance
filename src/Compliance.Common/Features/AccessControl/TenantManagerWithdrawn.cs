using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The tenant manager guard recorded, before the source change commits, that a member stops
///     counting as an active administrator for <paramref name="Reason" />.
/// </summary>
[Discriminator("bdgrz.rbac.manager_guard.withdrawn", 1)]
public sealed record TenantManagerWithdrawn(Uuid TenantId, Uuid MemberId, string Reason) : DomainEvent;
