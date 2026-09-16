using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

sealed class DeveloperIdentityContinuationSessionBehavior(UserSessionCookie session)
    : IRequestPipelineBehavior<ContinueWithDeveloperIdentity, AuthenticatedUserIdentity>
{
    public ValueTask<Result<AuthenticatedUserIdentity>> HandleAsync(
        IRequestContext<ContinueWithDeveloperIdentity> context,
        RequestPipelineNext<AuthenticatedUserIdentity> continuation,
        CancellationToken ct) => session.HandleAsync(continuation, ct);
}
