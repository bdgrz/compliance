using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed partial class TenantInvitationDeliveryReactor(
    IProjectionCheckpointStore checkpoints,
    IAggregateReader reader,
    IAggregateWriter writer,
    ITenantInvitationDelivery delivery,
    EmailChallengeTokenKeys tokenKeys,
    TimeProvider clock)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("tenant-invitations")),
      IReactorHandler<TenantMemberInvited>
{
    public async ValueTask HandleAsync(IReactorContext<TenantMemberInvited> context,
        CancellationToken ct)
    {
        var invited = context.Trigger;
        var attemptId = TenantInvitation.DeliveryAttemptFor(invited);
        if (clock.GetUtcNow() >= invited.ExpiresAt)
            return;
        var invitation = await reader.HydrateAsync(new TenantInvitation(invited.TenantId,
                invited.EmailAddress), ct).ConfigureAwait(false);
        if (invitation.IsAccepted || invitation.CurrentDeliveryAttemptId != attemptId ||
            invitation.DeliveryStatus is "delivered" or "failed")
            return;

        // Historical events contain only a hash. Their plaintext token cannot be
        // reconstructed, so mark the current attempt failed for an explicit reissue.
        if (invited.TokenKeyId is null)
        {
            await RecordOutcomeAsync(invited, context.Actor, current => current.RecordDeliveryFailure(
                attemptId, "key_unavailable", clock.GetUtcNow()),
                ct).ConfigureAwait(false);
            return;
        }

        if (!tokenKeys.TryDeriveInvitation(invited.TokenKeyId, attemptId,
                invited.TenantId, invited.EmailAddress, invited.ExpiresAt, out var token) ||
            !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(token!)),
                Convert.FromHexString(invited.TokenHash)))
        {
            await RecordOutcomeAsync(invited, context.Actor, current => current.RecordDeliveryFailure(
                attemptId, "key_unavailable", clock.GetUtcNow()),
                ct).ConfigureAwait(false);
            return;
        }

        try
        {
            await delivery.SendAsync(attemptId, invited.TenantId,
                invited.EmailAddress, token!, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await RecordOutcomeAsync(invited, context.Actor, current => current.RecordDeliveryFailure(
                attemptId, "delivery_failed", clock.GetUtcNow()),
                ct).ConfigureAwait(false);
            return;
        }

        await RecordOutcomeAsync(invited, context.Actor, current => current.RecordDeliverySent(
            attemptId, clock.GetUtcNow()), ct).ConfigureAwait(false);
    }

    async ValueTask RecordOutcomeAsync(TenantMemberInvited invited, ClaimsPrincipal actor,
        Func<TenantInvitation, Result> operation, CancellationToken ct)
    {
        try
        {
            var current = await reader.HydrateAsync(new TenantInvitation(invited.TenantId,
                invited.EmailAddress), ct).ConfigureAwait(false);
            if (current.IsAccepted ||
                current.CurrentDeliveryAttemptId != TenantInvitation.DeliveryAttemptFor(invited) ||
                current.DeliveryStatus is "delivered" or "failed")
                return;
            var result = operation(current);
            if (!result.IsSuccess)
                throw new InvalidOperationException();
            await writer.SaveAsync(current, new RequestDispatchContext(actor), ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Storage errors can contain a stream address derived from the recipient.
            throw new InvalidOperationException("Invitation delivery outcome could not be recorded.");
        }
    }
}
