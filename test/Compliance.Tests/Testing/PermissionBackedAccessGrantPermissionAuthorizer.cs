using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Adapts authorization test fixtures while production tests exercise the grant evaluator directly.</summary>
sealed class PermissionBackedAccessGrantPermissionAuthorizer(IPermissionAuthorizer permissions)
    : IAccessGrantPermissionAuthorizer
{
    public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, Uuid programId,
        string permission, CancellationToken ct = default) =>
        permissions.IsAllowedAsync(tenantId, userId, memberId, permission, ct);

    public async ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId,
        Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
        await permissions.IsAllowedAsync(tenantId, userId, memberId, permission, ct)
            .ConfigureAwait(false)
            ? new ProgramAccessVisibility(true, new HashSet<Uuid>())
            : new ProgramAccessVisibility(false, new HashSet<Uuid>());
}
