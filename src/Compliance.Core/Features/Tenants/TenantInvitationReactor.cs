using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed partial class TenantInvitationReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("tenant-invitations")),
      IReactorHandler<TenantInvitationAccepted>
{
    public async ValueTask HandleAsync(IReactorContext<TenantInvitationAccepted> context, CancellationToken ct)
    {
        await bus.SendReactionAsync(new RegisterMember(context.Trigger.TenantId, context.Trigger.UserId,
            context.Trigger.Affiliation), context, ct);
        if (context.Trigger.Administrator)
        {
            var memberId = RbacIds.Member(context.Trigger.TenantId, context.Trigger.UserId);
            await bus.SendReactionAsync(new AssignTeamMember(context.Trigger.TenantId,
                BuiltInRbac.AdministratorsTeamId(context.Trigger.TenantId), memberId), context, ct);
            await bus.SendReactionAsync(new ActivateTenant(context.Trigger.TenantId,
                context.Trigger.UserId, context.Trigger.EmailAddress), context, ct);
        }
    }
}
