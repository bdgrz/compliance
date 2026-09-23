using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class AcceptTenantInvitationHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<AcceptTenantInvitation>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AcceptTenantInvitation> context,
        CancellationToken ct)
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("AcceptTenantInvitationAuthorizer must reject this actor.");
        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalized))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a valid email address."));

        // The worker can record delivery on the same invitation stream while the
        // invitee accepts. Rehydrate on a stream conflict; Accept is idempotent for
        // the same user and still rejects a superseded or expired token.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await executor.ExecuteAsync(new TenantInvitation(context.Request.TenantId,
                        normalized),
                    invitation => AggregateOutcome.CommitOnSuccess(
                        invitation.Accept(userId, context.Request.Token, clock.GetUtcNow())),
                    context, ct).ConfigureAwait(false);
            }
            catch (EventStreamConcurrencyException) when (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)), ct)
                    .ConfigureAwait(false);
            }
        }
    }
}
