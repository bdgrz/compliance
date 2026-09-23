using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class IssueEmailChallengeHandler(
    IAggregateExecutor executor,
    EmailChallengeTokenKeys tokenKeys,
    TimeProvider clock) : IRequestHandler<IssueEmailChallenge>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<IssueEmailChallenge> context, CancellationToken ct)
    {
        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalized))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Enter a valid email address."));

        var challengeId = Uuid.CreateVersion4();
        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(15);
        var token = tokenKeys.Derive(tokenKeys.ActiveKeyId, challengeId,
            context.Request.UserId, normalized, expiresAt);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        return await executor.ExecuteAsync(
            new EmailAddress(normalized),
            address => AggregateOutcome.CommitOnSuccess(address.IssueChallenge(
                context.Request.UserId, challengeId, hash, expiresAt, now,
                tokenKeys.ActiveKeyId)),
            context, ct).ConfigureAwait(false);
    }
}
