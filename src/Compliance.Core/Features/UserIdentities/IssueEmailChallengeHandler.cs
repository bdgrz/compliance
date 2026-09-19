using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class IssueEmailChallengeHandler(
    IAggregateExecutor executor,
    IEmailChallengeDelivery delivery,
    TimeProvider clock) : IRequestHandler<IssueEmailChallenge>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<IssueEmailChallenge> context, CancellationToken ct)
    {
        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalized))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Enter a valid email address."));

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var now = clock.GetUtcNow();
        var result = await executor.ExecuteAsync(
            new EmailAddress(normalized),
            address => AggregateOutcome.CommitOnSuccess(address.IssueChallenge(
                context.Request.UserId, Uuid.CreateVersion4(), hash, now.AddMinutes(15), now)),
            context, ct).ConfigureAwait(false);
        if (result.IsSuccess)
            await delivery.SendAsync(context.Request.UserId, normalized, token, ct).ConfigureAwait(false);
        return result;
    }
}
