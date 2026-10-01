using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>The tenant manager guard cleared an earlier withdrawal for <paramref name="Reason" />.</summary>
[Discriminator("bdgrz.rbac.manager_guard.restored", 1)]
public sealed record TenantManagerRestored(Uuid TenantId, Uuid MemberId, string Reason) : DomainEvent;
