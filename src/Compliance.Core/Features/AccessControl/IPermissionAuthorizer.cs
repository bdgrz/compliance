using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public interface IPermissionAuthorizer
{
    ValueTask<bool> IsAllowedAsync(
        Uuid tenantId,
        Uuid memberId,
        string permission,
        CancellationToken ct = default);
}
