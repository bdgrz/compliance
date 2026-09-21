using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed partial class TenantInvitationIssuer(IAggregateExecutor executor,
    ITenantInvitationDelivery delivery, TimeProvider clock,
    ILogger<TenantInvitationIssuer> logger)
{
    public async ValueTask<Result> IssueAsync<TRequest>(IRequestContext<TRequest> context,
        Uuid tenantId, string emailAddress, string affiliation, bool administrator,
        string? builtInRole, Uuid invitedBy, CancellationToken ct)
        where TRequest : IRequestBase
    {
        if (!EmailAddresses.TryNormalize(emailAddress, out var normalized))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a valid email address."));
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var now = clock.GetUtcNow();
        var result = await executor.ExecuteAsync(new TenantInvitation(tenantId, normalized),
            invitation => AggregateOutcome.CommitOnSuccess(invitation.Invite(affiliation,
                administrator, hash, now.AddDays(7), now, invitedBy, builtInRole,
                context.RequestId)),
            context, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return result;
        try
        {
            await delivery.SendAsync(tenantId, normalized, token, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException ||
                                           !ct.IsCancellationRequested)
        {
            try
            {
                await RecordOutcomeAsync(context, tenantId, normalized,
                    invitation => invitation.RecordDeliveryFailure(context.RequestId,
                        "delivery_failed", clock.GetUtcNow()), "failed", CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception outcomeException)
            {
                LogDeliveryOutcomeFailure(logger, outcomeException, tenantId, normalized);
            }
            throw;
        }
        await RecordOutcomeAsync(context, tenantId, normalized,
            invitation => invitation.RecordDeliverySent(context.RequestId, clock.GetUtcNow()),
            "sent", ct).ConfigureAwait(false);
        return result;
    }

    async ValueTask RecordOutcomeAsync<TRequest>(IRequestContext<TRequest> context,
        Uuid tenantId, string emailAddress, Func<TenantInvitation, Result> operation,
        string outcome, CancellationToken ct) where TRequest : IRequestBase
    {
        // Portia deliberately keeps aggregate commit and external delivery as two durability
        // boundaries. A crash after delivery and before this event leaves the attempt pending;
        // reissuing creates a new token and makes the retry explicit without storing plaintext.
        var result = await executor.ExecuteAsync(new TenantInvitation(tenantId, emailAddress),
            invitation => AggregateOutcome.CommitOnSuccess(operation(invitation)), context, ct)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
            throw new InvalidOperationException(
                $"The invitation delivery outcome could not be recorded: {outcome}.");
    }

    [LoggerMessage(EventId = 18301, Level = LogLevel.Error,
        Message = "Could not persist invitation delivery failure for {TenantId}/{EmailAddress}.")]
    static partial void LogDeliveryOutcomeFailure(ILogger logger, Exception exception,
        Uuid tenantId, string emailAddress);
}
