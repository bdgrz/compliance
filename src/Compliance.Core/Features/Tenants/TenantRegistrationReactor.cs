using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed partial class TenantRegistrationReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenants")),
      IReactorHandler<TenantRegistered>,
      IReactorHandler<TenantSlugChangeRequested>,
      IReactorHandler<TenantSlugChanged>,
      IReactorHandler<TenantSlugSurrenderRequested>
{
    public async ValueTask HandleAsync(IReactorContext<TenantRegistered> context, CancellationToken ct)
    {
        await bus.SendReactionAsync(
            new RegisterTenantSlug(context.Trigger.TenantId, context.Trigger.Slug), context, ct);
        await bus.SendReactionAsync(
            new RegisterTenantOwner(context.Trigger.TenantId, context.Trigger.OwnerUserId), context, ct);
        if (context.Trigger.FirstAdministratorEmail is { } email)
            await bus.SendReactionAsync(new InviteTenantMember(context.Trigger.TenantId, email,
                "client_personnel", Administrator: true),
                context, ct);
    }

    public ValueTask HandleAsync(IReactorContext<TenantSlugSurrenderRequested> context, CancellationToken ct) =>
        bus.SendReactionAsync(
            new SurrenderTenantSlug(context.Trigger.TenantId, context.Trigger.Slug), context, ct);

    public ValueTask HandleAsync(IReactorContext<TenantSlugChangeRequested> context, CancellationToken ct) =>
        bus.SendReactionAsync(
            new RegisterTenantSlug(context.Trigger.TenantId, context.Trigger.NewSlug), context, ct);

    public ValueTask HandleAsync(IReactorContext<TenantSlugChanged> context, CancellationToken ct) =>
        bus.SendReactionAsync(
            new SurrenderTenantSlug(context.Trigger.TenantId, context.Trigger.OldSlug), context, ct);
}
