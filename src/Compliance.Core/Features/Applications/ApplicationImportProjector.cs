using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class ApplicationImportProjector(IApplicationImportDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("application_imports"),
        "ApplicationImportDirectoryV1"), IProjectorHandler<ApplicationImportStaged>,
        IProjectorHandler<ApplicationImportCanceled>, IProjectorHandler<ApplicationImportRowCorrelated>,
        IProjectorHandler<ApplicationImportPlanStarted>, IProjectorHandler<ApplicationImportPlanRowFrozen>,
        IProjectorHandler<ApplicationImportPlanSealed>, IProjectorHandler<ApplicationImportCommitted>,
        IProjectorHandler<ApplicationImportRetirementProposalStarted>,
        IProjectorHandler<ApplicationImportRetirementRowFrozen>, IProjectorHandler<ApplicationImportRetirementProposalSealed>
{
    public ValueTask HandleAsync(ApplicationImportStaged ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationImportCanceled ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationImportRowCorrelated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationImportPlanStarted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationImportPlanRowFrozen ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationImportPlanSealed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationImportRetirementProposalStarted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationImportRetirementRowFrozen ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationImportRetirementProposalSealed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationImportCommitted ev, IProjectorContext context,
        CancellationToken ct)
    {
        if (context.Identity.Pattern.Realm != ev.TenantId.ToString())
            throw new InvalidOperationException("An import commit must belong to its tenant projection workload.");
        return projection.ApplyAsync(ev, ct);
    }
}
