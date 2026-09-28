using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Retries recovery completion notices independently from challenge delivery.</summary>
public sealed partial class IdentityRecoveryNoticeReactor(
    IProjectionCheckpointStore checkpoints,
    IEmailChallengeDelivery delivery)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "email-addresses")),
      IReactorHandler<IdentityRecoveryChallengeCompleted>
{
    public async ValueTask HandleAsync(IReactorContext<IdentityRecoveryChallengeCompleted> context,
        CancellationToken ct)
    {
        var completed = context.Trigger;
        try
        {
            await delivery.SendRecoveryCompletedAsync(completed.ChallengeId, completed.UserId,
                completed.EmailAddress, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Propagate a safe error so this dedicated checkpoint retries the stable effect.
            throw new InvalidOperationException("Identity recovery notice delivery failed.");
        }
    }
}
