using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class CompleteEmailChallengeHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<CompleteEmailChallenge>
{
    public ValueTask<Result> HandleAsync(IRequestContext<CompleteEmailChallenge> context, CancellationToken ct)
    {
        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalized))
            return ValueTask.FromResult(Result.Failure(new RequestError(
                RequestErrorKind.Validation, "Enter a valid email address.")));

        return executor.ExecuteAsync(
            new EmailAddress(normalized),
            address => AggregateOutcome.CommitOnSuccess(address.CompleteChallenge(
                context.Request.UserId, context.Request.Token, clock.GetUtcNow())),
            context, ct);
    }
}
