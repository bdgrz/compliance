using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

sealed class OidcContinuationSessionBehavior(UserSessionCookie session)
    : IRequestPipelineBehavior<ContinueWithOidcProvider, AuthenticatedUserIdentity>
{
    public ValueTask<Result<AuthenticatedUserIdentity>> HandleAsync(
        IRequestContext<ContinueWithOidcProvider> context,
        RequestPipelineNext<AuthenticatedUserIdentity> continuation,
        CancellationToken ct) => session.HandleAsync(continuation, ct);
}
