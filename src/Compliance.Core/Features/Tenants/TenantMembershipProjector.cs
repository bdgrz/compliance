using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed partial class TenantMembershipProjector(ITenantMembershipDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("rbac-members"), "TenantMembership"),
      IProjectorHandler<MemberRegistered>,
      IProjectorHandler<MemberSuspended>,
      IProjectorHandler<MemberReinstated>
{
    public ValueTask HandleAsync(MemberRegistered ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(MemberSuspended ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(MemberReinstated ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
