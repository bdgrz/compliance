using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed partial class TenantInvitationDirectoryProjector(
    ITenantInvitationDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("tenant-invitations"),
      "TenantInvitationDirectory"),
      IProjectorHandler<TenantMemberInvited>, IProjectorHandler<TenantInvitationAccepted>,
      IProjectorHandler<TenantInvitationDeliverySent>,
      IProjectorHandler<TenantInvitationDeliveryFailed>
{
    public ValueTask HandleAsync(TenantMemberInvited ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantInvitationAccepted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantInvitationDeliverySent ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantInvitationDeliveryFailed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
