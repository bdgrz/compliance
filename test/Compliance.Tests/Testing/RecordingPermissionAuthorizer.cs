using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Answers every permission check the same way and records the permissions asked for.</summary>
sealed class RecordingPermissionAuthorizer(bool allowed) : IPermissionAuthorizer
{
    public List<string> Permissions { get; } = [];

    public List<Uuid> MemberIds { get; } = [];

    public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
        string permission, CancellationToken ct = default)
    {
        Permissions.Add(permission);
        MemberIds.Add(memberId);
        return ValueTask.FromResult(allowed);
    }
}
