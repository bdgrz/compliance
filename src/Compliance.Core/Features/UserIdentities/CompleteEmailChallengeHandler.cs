using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class CompleteEmailChallengeHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<CompleteEmailChallenge>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CompleteEmailChallenge> context,
        CancellationToken ct)
    {
        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalized))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a valid email address."));

        // The delivery reactor records its outcome on this stream. A user may complete the
        // challenge at the same time, so reload and re-evaluate this idempotent operation.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await executor.ExecuteAsync(
                    new EmailAddress(normalized),
                    address => AggregateOutcome.CommitOnSuccess(address.CompleteChallenge(
                        context.Request.UserId, context.Request.Token, clock.GetUtcNow())),
                    context, ct).ConfigureAwait(false);
            }
            catch (EventStreamConcurrencyException) when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10 * attempt), ct)
                    .ConfigureAwait(false);
            }
        }
    }
}
