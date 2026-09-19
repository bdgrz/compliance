using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class LinkOidcProviderIdentityHandler(IAggregateExecutor executor)
    : IRequestHandler<LinkOidcProviderIdentity, AuthenticatedUserIdentity>
{
    public ValueTask<Result<AuthenticatedUserIdentity>> HandleAsync(
        IRequestContext<LinkOidcProviderIdentity> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) ||
            !OidcProviderClaims.TryGet(context.Actor, out var provider, out var identifier,
                out var emailAddress))
            return ValueTask.FromResult(Result<AuthenticatedUserIdentity>.Failure(
                new RequestError(RequestErrorKind.Unauthorized,
                    "Linking an OIDC identity requires the current browser session and provider proof.")));

        return executor.ExecuteAsync(new UserIdentity(provider, identifier),
            identity => AggregateOutcome.CommitOnSuccess(identity.Register(userId, emailAddress)),
            context, ct);
    }
}
