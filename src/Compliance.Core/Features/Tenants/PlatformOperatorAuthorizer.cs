using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Only an explicitly authorized platform operator may provision a tenant.</summary>
sealed class PlatformOperatorAuthorizer(PlatformOperatorAuthority operators) : IRequestAuthorizer<IPlatformOperatorRequest>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IPlatformOperatorRequest> context, CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor))
            return ValueTask.FromResult(Result.Success);
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return ValueTask.FromResult(Result.Failure(new RequestError(
                RequestErrorKind.Unauthorized, "Tenant registration requires a Bdgrz user identity.")));

        return ValueTask.FromResult(operators.IsOperator(userId)
            ? Result.Success
            : Result.Failure(new RequestError(
                RequestErrorKind.Forbidden, "The actor is not a platform operator.")));
    }
}
