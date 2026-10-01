using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

static class WorkforceSourceComparisonAccess
{
    public static async ValueTask<Result> CheckAsync(IPermissionAuthorizer permissions, Uuid tenantId,
        ClaimsPrincipal actor, bool requiresRestrictedFields, CancellationToken ct)
    {
        if (!requiresRestrictedFields)
            return Result.Success;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(actor, out var subject)
            ? subject : throw new InvalidOperationException("WorkforceAuthorizer must reject this actor.");
        var manager = await FieldRestrictions.ForActorAsync(permissions, tenantId, userId,
            FieldClasses.WorkforceManagerChain, ct).ConfigureAwait(false);
        var reason = await FieldRestrictions.ForActorAsync(permissions, tenantId, userId,
            FieldClasses.WorkforcePersonalDetails, ct).ConfigureAwait(false);
        return manager.CanRead && reason.CanRead
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Comparing work relationship source facts requires both restricted field read grants."));
    }
}
