using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed partial class TenantDirectoryProjector(ITenantDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForPattern("bdgrz", "tenants"), "TenantDirectory"),
      IProjectorHandler<TenantRegistered>,
      IProjectorHandler<TenantSlugConfirmed>,
      IProjectorHandler<TenantSlugRejected>,
      IProjectorHandler<TenantSuspended>,
      IProjectorHandler<TenantReactivated>,
      IProjectorHandler<TenantActivated>,
      IProjectorHandler<TenantSlugChanged>
{
    public ValueTask HandleAsync(TenantRegistered ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantSlugConfirmed ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantSlugRejected ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantSuspended ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantReactivated ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantActivated ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantSlugChanged ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
