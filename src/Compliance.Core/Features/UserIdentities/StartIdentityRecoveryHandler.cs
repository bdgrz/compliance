using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class StartIdentityRecoveryHandler(
    IAggregateExecutor executor,
    EmailChallengeTokenKeys tokenKeys,
    TimeProvider clock) : IRequestHandler<StartIdentityRecovery>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<StartIdentityRecovery> context, CancellationToken ct)
    {
        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalized))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a valid email address."));

        var challengeId = Uuid.CreateVersion4();
        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(15);
        var address = new EmailAddress(normalized);
        return await executor.ExecuteAsync(address, current =>
        {
            if (current.Owner is not { } owner || !current.IsVerified)
                return AggregateOutcome.Commit(Result.Success);

            var token = tokenKeys.DeriveRecovery(tokenKeys.ActiveKeyId, challengeId,
                owner, normalized, expiresAt);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
            return AggregateOutcome.CommitOnSuccess(current.IssueRecoveryChallenge(owner,
                challengeId, hash, expiresAt, now, tokenKeys.ActiveKeyId));
        }, context, ct).ConfigureAwait(false);
    }
}
