using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed partial class EmailChallengeDeliveryReactor(
    IProjectionCheckpointStore checkpoints,
    IAggregateReader reader,
    IAggregateWriter writer,
    IEmailChallengeDelivery delivery,
    EmailChallengeTokenKeys tokenKeys,
    TimeProvider clock)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "email-addresses")),
      IReactorHandler<EmailChallengeIssued>
{
    public async ValueTask HandleAsync(IReactorContext<EmailChallengeIssued> context,
        CancellationToken ct)
    {
        var issued = context.Trigger;
        if (clock.GetUtcNow() >= issued.ExpiresAt)
            return;
        var address = await reader.HydrateAsync(new EmailAddress(issued.EmailAddress), ct)
            .ConfigureAwait(false);
        if (address.IsVerified || address.CurrentChallengeId != issued.ChallengeId ||
            address.DeliveryStatus is "delivered" or "failed")
            return;

        // Events written before durable delivery have no derivation key. Their original
        // plaintext token cannot be recovered; let the owner reissue without blocking the
        // global reactor behind this historical event until it expires.
        if (issued.TokenKeyId is null)
        {
            await RecordFailureAsync(issued, context.Actor, "key_unavailable", ct)
                .ConfigureAwait(false);
            return;
        }

        if (!tokenKeys.TryDerive(issued.TokenKeyId, issued.ChallengeId, issued.UserId,
                issued.EmailAddress, issued.ExpiresAt, out var token) ||
            !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(token!)),
                Convert.FromHexString(issued.TokenHash)))
        {
            await RecordFailureAsync(issued, context.Actor, "key_unavailable", ct)
                .ConfigureAwait(false);
            return;
        }

        try
        {
            await delivery.SendAsync(issued.ChallengeId, issued.UserId, issued.EmailAddress,
                token!, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await RecordFailureAsync(issued, context.Actor, "delivery_failed", ct)
                .ConfigureAwait(false);
            return;
        }

        await RecordOutcomeAsync(issued, context.Actor,
            address => address.RecordDeliverySent(issued.ChallengeId, clock.GetUtcNow()), ct)
            .ConfigureAwait(false);
    }

    async ValueTask RecordFailureAsync(EmailChallengeIssued issued, ClaimsPrincipal actor,
        string code, CancellationToken ct) =>
        await RecordOutcomeAsync(issued, actor,
            address => address.RecordDeliveryFailure(issued.ChallengeId, code, clock.GetUtcNow()), ct)
            .ConfigureAwait(false);

    async ValueTask RecordOutcomeAsync(EmailChallengeIssued issued, ClaimsPrincipal actor,
        Func<EmailAddress, Result> operation, CancellationToken ct)
    {
        try
        {
            var address = await reader.HydrateAsync(new EmailAddress(issued.EmailAddress), ct)
                .ConfigureAwait(false);
            if (address.IsVerified || address.CurrentChallengeId != issued.ChallengeId ||
                address.DeliveryStatus is "delivered" or "failed")
                return;
            var result = operation(address);
            if (!result.IsSuccess)
                throw new InvalidOperationException();
            await writer.SaveAsync(address, new RequestDispatchContext(actor), ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Event-store errors can contain the stream address (an email-derived ID).
            throw new InvalidOperationException("Email challenge delivery outcome could not be recorded.");
        }
    }
}
