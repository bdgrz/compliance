using Cntryl.Portia;
using Microsoft.Extensions.Logging;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed partial class TenantSelfServiceActivationReactor(IProjectionCheckpointStore checkpoints,
    IAggregateReader reader, IRequestBus bus, ILogger<TenantSelfServiceActivationReactor> logger,
    TenantActivationPolicy? policy = null)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("rbac-team-members")),
      IReactorHandler<TeamMemberAssigned>
{
    readonly TenantActivationPolicy _policy = policy ?? TenantActivationPolicy.Default;

    public ValueTask HandleAsync(IReactorContext<TeamMemberAssigned> context, CancellationToken ct) =>
        _policy.ReactAsync(nameof(TenantSelfServiceActivationReactor), context.Trigger.TenantId,
            context.Trigger, logger, token => ReactAsync(context, token), ct);

    async ValueTask ReactAsync(IReactorContext<TeamMemberAssigned> context, CancellationToken ct)
    {
        var assignment = context.Trigger;
        if (assignment.TeamId != BuiltInRbac.AdministratorsTeamId(assignment.TenantId))
            return;

        var tenant = await reader.HydrateAsync(new Tenant(assignment.TenantId), ct);
        if (!tenant.NeedsCreatorActivation ||
            assignment.MemberId != RbacIds.Member(assignment.TenantId, tenant.OwnerUserId))
            return;

        await _policy.SendActivationAsync(bus,
            new ActivateTenant(assignment.TenantId, tenant.OwnerUserId), context, logger, ct);
    }
}
