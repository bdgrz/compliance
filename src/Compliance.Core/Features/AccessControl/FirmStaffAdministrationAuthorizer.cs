using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Platform staff metadata only; operator status cannot supply professional duties or client access.</summary>
sealed class FirmStaffAdministrationAuthorizer(IPlatformOperatorAccess operators)
    : IRequestAuthorizer<IFirmStaffAdministrationRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IFirmStaffAdministrationRequest> context,
        CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Staff directory administration requires an authenticated current operator."));
        return await operators.IsOperatorAsync(userId, ct).ConfigureAwait(false)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The actor is not a platform operator."));
    }
}
