using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed partial class TenantRegistrationReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenants")),
      IReactorHandler<TenantRegistered>,
      IReactorHandler<TenantSlugSurrenderRequested>
{
    public async ValueTask HandleAsync(IReactorContext<TenantRegistered> context, CancellationToken ct)
    {
        await bus.SendReactionAsync(
            new RegisterTenantSlug(context.Trigger.TenantId, context.Trigger.Slug), context, ct);
        await bus.SendReactionAsync(
            new RegisterTenantOwner(context.Trigger.TenantId, context.Trigger.OwnerUserId), context, ct);
    }

    public ValueTask HandleAsync(IReactorContext<TenantSlugSurrenderRequested> context, CancellationToken ct) =>
        bus.SendReactionAsync(
            new SurrenderTenantSlug(context.Trigger.TenantId, context.Trigger.Slug), context, ct);
}
