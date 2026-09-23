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
            address.DeliveryStatus == "delivered")
            return;

        if (!tokenKeys.TryDerive(issued.TokenKeyId, issued.ChallengeId, issued.UserId,
                issued.EmailAddress, issued.ExpiresAt, out var token) ||
            !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(token!)),
                Convert.FromHexString(issued.TokenHash)))
        {
            await RecordFailureAsync(issued, "key_unavailable", ct).ConfigureAwait(false);
            throw new InvalidOperationException("Email challenge token key is unavailable.");
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
            await RecordFailureAsync(issued, "delivery_failed", ct).ConfigureAwait(false);
            throw new InvalidOperationException("Email challenge delivery failed.");
        }

        await RecordOutcomeAsync(issued,
            address => address.RecordDeliverySent(issued.ChallengeId, clock.GetUtcNow()), ct)
            .ConfigureAwait(false);
    }

    async ValueTask RecordFailureAsync(EmailChallengeIssued issued, string code, CancellationToken ct) =>
        await RecordOutcomeAsync(issued,
            address => address.RecordDeliveryFailure(issued.ChallengeId, code, clock.GetUtcNow()), ct)
            .ConfigureAwait(false);

    async ValueTask RecordOutcomeAsync(EmailChallengeIssued issued,
        Func<EmailAddress, Result> operation, CancellationToken ct)
    {
        try
        {
            var address = await reader.HydrateAsync(new EmailAddress(issued.EmailAddress), ct)
                .ConfigureAwait(false);
            var result = operation(address);
            if (!result.IsSuccess)
                throw new InvalidOperationException();
            await writer.SaveAsync(address, new RequestDispatchContext(RequestActor.System), ct)
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
