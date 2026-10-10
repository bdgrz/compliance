using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

/// <summary>Grants every permission to the allowed users and none to anyone else.</summary>
sealed class MemberPermissions : IPermissionAuthorizer
{
    readonly HashSet<Uuid> _allowed = [];
    readonly HashSet<(Uuid UserId, string Permission)> _denied = [];
    readonly HashSet<Uuid> _restrictedReadAllowed = [];

    public void Allow(Uuid userId) => _allowed.Add(userId);
    public void Deny(Uuid userId, string permission) => _denied.Add((userId, permission));
    public void AllowRestrictedRead(Uuid userId) => _restrictedReadAllowed.Add(userId);
    public void DenyRestrictedRead(Uuid userId) => _restrictedReadAllowed.Remove(userId);

    public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
        string permission, CancellationToken ct = default) =>
        ValueTask.FromResult(permission == RbacPermissions.ApplicationRestrictedRead
            ? _restrictedReadAllowed.Contains(userId)
            : _allowed.Contains(userId) && !_denied.Contains((userId, permission)));
}
