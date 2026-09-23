using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class GetEmailChallengeStatusHandler(IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<GetEmailChallengeStatus, EmailChallengeStatusView>
{
    public async ValueTask<Result<EmailChallengeStatusView>> HandleAsync(
        IRequestContext<GetEmailChallengeStatus> context, CancellationToken ct)
    {
        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalized))
            return Result<EmailChallengeStatusView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a valid email address."));

        var address = await reader.HydrateAsync(new EmailAddress(normalized), ct).ConfigureAwait(false);
        return address.Owner == context.Request.UserId
            ? Result<EmailChallengeStatusView>.Success(address.GetChallengeStatus(clock.GetUtcNow()))
            : Result<EmailChallengeStatusView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The email address was not found."));
    }
}
