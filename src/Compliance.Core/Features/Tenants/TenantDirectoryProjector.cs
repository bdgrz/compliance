using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed partial class TenantDirectoryProjector(ITenantDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForPattern("bdgrz", "tenants"), "TenantDirectory"),
      IProjectorHandler<TenantRegistered>
{
    public ValueTask HandleAsync(TenantRegistered ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
