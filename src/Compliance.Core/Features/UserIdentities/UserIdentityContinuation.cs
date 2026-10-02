using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class UserIdentityContinuation(IAggregateExecutor executor, TimeProvider? clock = null)
{
    public async ValueTask<Result<AuthenticatedUserIdentity>> ContinueAsync(
        string provider,
        string identifier,
        string? emailAddress,
        Uuid? existingUserId,
        IExecutionContext context,
        CancellationToken ct,
        bool observeProfile = false,
        string? displayName = null)
    {
        var authenticated = await executor.ExecuteAsync(
            new UserIdentity(provider, identifier),
            identity => AggregateOutcome.CommitOnSuccess(identity.IsRegistered
                ? identity.Authenticate()
                : identity.Register(existingUserId, emailAddress)),
            context, ct).ConfigureAwait(false);
        if (!authenticated.IsSuccess || !observeProfile)
            return authenticated;

        // Portia deliberately separates authentication audits from state-changing events.
        // Rehydrate before recording presentation so an intervening revocation fails closed.
        return await executor.ExecuteAsync(new UserIdentity(provider, identifier),
            identity => AggregateOutcome.CommitOnSuccess(identity.ObserveAuthenticatedProfile(
                displayName, (clock ?? TimeProvider.System).GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
