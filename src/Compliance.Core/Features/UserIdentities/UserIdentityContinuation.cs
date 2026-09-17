using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class UserIdentityContinuation(IAggregateExecutor executor)
{
    public ValueTask<Result<AuthenticatedUserIdentity>> ContinueAsync(
        string provider,
        string identifier,
        string? emailAddress,
        Uuid? existingUserId,
        IExecutionContext context,
        CancellationToken ct) =>
        executor.ExecuteAsync(
            new UserIdentity(provider, identifier),
            identity => identity.IsRegistered
                ? AggregateOutcome.CommitOnSuccess(identity.Authenticate())
                : AggregateOutcome.CommitOnSuccess(identity.Register(existingUserId, emailAddress)),
            context, ct);
}
