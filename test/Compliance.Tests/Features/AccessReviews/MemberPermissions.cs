using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

/// <summary>Grants every permission to the allowed users and none to anyone else.</summary>
sealed class MemberPermissions : IPermissionAuthorizer
{
    readonly HashSet<Uuid> _allowed = [];

    public void Allow(Uuid userId) => _allowed.Add(userId);

    public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
        string permission, CancellationToken ct = default) =>
        ValueTask.FromResult(_allowed.Contains(userId));
}
