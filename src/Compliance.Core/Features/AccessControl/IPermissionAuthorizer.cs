using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public interface IPermissionAuthorizer
{
    ValueTask<bool> IsAllowedAsync(
        Uuid tenantId,
        Uuid userId,
        Uuid memberId,
        string permission,
        CancellationToken ct = default);
}
