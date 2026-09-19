using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class ReserveEmailHandler(IAggregateExecutor executor) : IRequestHandler<ReserveEmail>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReserveEmail> context, CancellationToken ct)
    {
        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalized))
            return ValueTask.FromResult(Result.Failure(new RequestError(
                RequestErrorKind.Validation, "Enter a valid email address.")));

        return executor.ExecuteAsync(
            new EmailAddress(normalized),
            address => AggregateOutcome.CommitOnSuccess(address.Reserve(context.Request.UserId)),
            context, ct);
    }
}
