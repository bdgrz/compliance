using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     A governed non-human identity. <c>Unowned</c> is evaluated when read: it is true with
///     <c>UnownedReasons</c> of <c>review_expired</c>, <c>owner_relationship_ended</c>,
///     <c>owner_not_on_roster</c>, or <c>owner_team_deleted</c>. Retired identities are never
///     unowned work.
/// </summary>
public sealed record ServiceIdentityView(Uuid TenantId, Uuid ServiceIdentityId, long Revision,
    string DisplayName, string IdentityKind, string Purpose, string? Environment,
    string LifecycleStatus, string OwnerKind, Uuid OwnerId, DateOnly ReviewBy, string SourceKind,
    bool Unowned, IReadOnlyList<string> UnownedReasons, ActorReference LastChangedBy,
    DateTimeOffset LastChangedAt);
