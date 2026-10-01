using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Links a roster person to one platform member, or clears the link when <c>UserId</c> is null.
///     The link is an attributable reconciliation fact; it never grants or revokes platform access.
/// </summary>
[Discriminator("bdgrz.workforce.person.membership-correlated", 1)]
public sealed record PersonMembershipCorrelated(Uuid TenantId, Uuid PersonId, long Revision,
    Uuid? UserId, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;
