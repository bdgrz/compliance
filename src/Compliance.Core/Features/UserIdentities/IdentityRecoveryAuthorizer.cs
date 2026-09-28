using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

sealed class IdentityRecoveryAuthorizer : IRequestAuthorizer<CompleteIdentityRecovery>
{
    public ValueTask<Result> AuthorizeAsync(
        IRequestContext<CompleteIdentityRecovery> context, CancellationToken ct)
    {
        _ = ct;
        return ValueTask.FromResult(OidcProviderClaims.TryGet(context.Actor,
            out _, out _, out _)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Completing identity recovery requires proof of the replacement provider identity.")));
    }
}
