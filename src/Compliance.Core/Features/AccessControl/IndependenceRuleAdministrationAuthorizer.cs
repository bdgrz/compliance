using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Platform-content authority only; authors draft versions without ratifying or reading client facts.</summary>
sealed class IndependenceRuleAdministrationAuthorizer(IPlatformOperatorAccess operators)
    : IRequestAuthorizer<IIndependenceRuleAdministrationRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IIndependenceRuleAdministrationRequest> context,
        CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Draft rule administration requires an authenticated platform operator."));
        return await operators.IsOperatorAsync(userId, ct).ConfigureAwait(false)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The actor is not a platform operator."));
    }
}
