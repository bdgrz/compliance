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
        var memberId = RbacIds.Member(context.Trigger.TenantId, context.Trigger.UserId);
        if (context.Trigger.BuiltInRole is { } role)
        {
            var teamId = BuiltInRbac.TeamIdForRole(context.Trigger.TenantId, role) ??
                throw new InvalidOperationException("The accepted invitation has an unknown built-in role.");
            await bus.SendReactionAsync(new AssignTeamMember(context.Trigger.TenantId,
                teamId, memberId), context, ct);
        }
        if (context.Trigger.Administrator)
        {
            await bus.SendReactionAsync(new AssignTeamMember(context.Trigger.TenantId,
                BuiltInRbac.AdministratorsTeamId(context.Trigger.TenantId), memberId), context, ct);
            await bus.SendReactionAsync(new ActivateTenant(context.Trigger.TenantId,
                context.Trigger.UserId, context.Trigger.EmailAddress), context, ct);
        }
    }
}
