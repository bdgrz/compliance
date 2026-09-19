using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed partial class EmailReservationReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "user-identities")),
      IReactorHandler<UserIdentityRegistered>
{
    public async ValueTask HandleAsync(IReactorContext<UserIdentityRegistered> context, CancellationToken ct)
    {
        if (context.Trigger.EmailAddress is not { } address)
            return;

        var result = await bus.SendAsync(new ReserveEmail(context.Trigger.UserId, address), context, ct)
            .ConfigureAwait(false);
        // A second provider identity may assert an address already owned by another user. That
        // conflict is terminal for this event; retrying it would block all later registrations.
        if (!result.IsSuccess && result.Error.Kind != RequestErrorKind.Conflict)
            throw new ReactionCommandFailedException(result.Error);
    }
}
