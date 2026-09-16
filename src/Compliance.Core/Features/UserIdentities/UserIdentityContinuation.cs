using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class UserIdentityContinuation(IAggregateRepository repository)
{
    public async ValueTask<Result<AuthenticatedUserIdentity>> ContinueAsync(
        string provider,
        string identifier,
        string? emailAddress,
        Uuid? existingUserId,
        IExecutionContext context,
        CancellationToken ct)
    {
        var identity = await repository.HydrateAsync(new UserIdentity(provider, identifier), ct);
        if (identity.IsRegistered)
        {
            var authentication = identity.Authenticate();
            if (!authentication.IsSuccess)
            {
                return authentication;
            }

            await repository.SaveAsync(identity, context, ct);
            return authentication;
        }

        var registration = identity.Register(existingUserId, emailAddress);
        if (!registration.IsSuccess)
        {
            return Result<AuthenticatedUserIdentity>.Failure(registration.Error);
        }

        await repository.SaveAsync(identity, context, ct);
        return registration;
    }
}
