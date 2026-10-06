using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class ApplicationDirectoryProjector(IApplicationDirectoryProjection projection, IAggregateReader reader)
    : Projector(projection, EventStreamPattern.ForTenant(), "ApplicationDirectoryV2"),
      IProjectorHandler<ApplicationDeclared>, IProjectorHandler<ApplicationRevised>,
      IProjectorHandler<SystemInstanceDeclared>, IProjectorHandler<SystemInstanceRegistered>,
      IProjectorHandler<ApplicationRetired>, IProjectorHandler<SystemInstanceRetired>,
      IProjectorHandler<ApplicationImportCommitted>
{
    public ValueTask HandleAsync(ApplicationDeclared ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(SystemInstanceDeclared ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationRetired ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(SystemInstanceRetired ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(SystemInstanceRegistered ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public async ValueTask HandleAsync(ApplicationImportCommitted ev, IProjectorContext context,
        CancellationToken ct)
    {
        if (context.Identity.Pattern.Realm != ev.TenantId.ToString())
            throw new InvalidOperationException("An import commit must belong to its tenant projection workload.");
        var ledger = await reader.HydrateAsync(new ApplicationImportLedger(ev.TenantId,
            ev.SourceKey, ev.SourceNamespace), ct).ConfigureAwait(false);
        var marker = ledger.GetCommit(ev.BatchId);
        var plan = ledger.GetFrozenPlan(ev.BatchId);
        if (!ledger.IsCommitDurable(ev.BatchId) || marker is null || plan is null ||
            marker with { Effects = ev.Effects } != ev || !marker.Effects.SequenceEqual(ev.Effects))
            throw new InvalidOperationException("The import projection requires a durable authoritative source commit.");
        foreach (var proof in marker.Effects)
        {
            var target = await reader.HydrateAsync(new DeclaredApplication(ev.TenantId, proof.ApplicationId), ct)
                .ConfigureAwait(false);
            var effect = target.GetPendingImportEffect(ev.BatchId, proof.RowId);
            if (effect is null || target.CommittedStreamPosition < proof.EventVersion || !ledger.IsEffectCommitted(effect))
                throw new InvalidOperationException("The committed import target proof is missing or differs from its source.");
        }
        await projection.ApplyCommittedImportAsync(ev, plan, ct).ConfigureAwait(false);
    }
}
