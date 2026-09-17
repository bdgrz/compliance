using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed partial class TenantSlugReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenant-slugs")),
      IReactorHandler<TenantSlugRegistered>,
      IReactorHandler<TenantSlugRegistrationRejected>,
      IReactorHandler<TenantSlugSurrendered>,
      IReactorHandler<TenantSlugSurrenderRejected>
{
    public ValueTask HandleAsync(IReactorContext<TenantSlugRegistered> context, CancellationToken ct) =>
        bus.SendReactionAsync(
            new ConfirmTenantSlug(context.Trigger.TenantId, context.Trigger.Slug), context, ct);

    public ValueTask HandleAsync(
        IReactorContext<TenantSlugRegistrationRejected> context,
        CancellationToken ct) => bus.SendReactionAsync(
        new RejectTenantSlug(context.Trigger.TenantId, context.Trigger.Slug), context, ct);

    public ValueTask HandleAsync(IReactorContext<TenantSlugSurrendered> context, CancellationToken ct) =>
        bus.SendReactionAsync(
            new ConfirmTenantSlugSurrender(context.Trigger.TenantId, context.Trigger.Slug), context, ct);

    public ValueTask HandleAsync(IReactorContext<TenantSlugSurrenderRejected> context, CancellationToken ct) =>
        bus.SendReactionAsync(
            new RejectTenantSlugSurrender(context.Trigger.TenantId, context.Trigger.Slug), context, ct);
}
