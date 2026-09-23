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

        // A recipient can use the token before the delivery reactor commits its
        // outcome on this stream. Allow roughly one second for that competing append,
        // reloading and re-evaluating this idempotent operation on each conflict.
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
            catch (EventStreamConcurrencyException) when (attempt < 9)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(
                    Math.Min(10 << (attempt - 1), 250)), ct)
                    .ConfigureAwait(false);
            }
        }
    }
}
