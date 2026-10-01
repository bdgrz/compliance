using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

sealed class FitzWorkforceSourceDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/workforce-sources-v1/projection", "WorkforceSourcesV1"),
        IWorkforceSourceDirectoryReader, IWorkforceSourceProjection
{
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("WorkforceSourcesV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "workforce-source-observations")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case WorkforceSourceObserved observed:
                await WorkforceSourceSchema.Observations.InsertAsync(Transaction,
                    new WorkforceSourceView(observed.TenantId, observed.ObservationId, 1, observed.Source,
                        observed.TargetKind, observed.TargetId, observed.ObservedTargetRevision, observed.Facts,
                        observed.ObservedAt, observed.Actor, observed.RecordedAt), ct).ConfigureAwait(false);
                break;
            case WorkforceSourceReconciled reconciled:
                var current = await WorkforceSourceSchema.Observations.GetAsync(Transaction,
                    reconciled.ObservationId, ct).ConfigureAwait(false);
                if (current is null || current.TenantId != reconciled.TenantId ||
                    current.Decision is not null || current.Revision + 1 != reconciled.Revision)
                    throw new InvalidOperationException("A source decision requires its unreconciled predecessor.");
                await WorkforceSourceSchema.Observations.ReplaceAsync(Transaction, current,
                    current with { Revision = reconciled.Revision, Decision = reconciled.Decision }, ct)
                    .ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<WorkforceSourceView?> GetAsync(Uuid tenantId, Uuid observationId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var view = await WorkforceSourceSchema.Observations.GetAsync(tx, observationId, ct).ConfigureAwait(false);
        return view?.TenantId == tenantId ? view : null;
    }

    public async ValueTask<Page<WorkforceSourceView>> ListAsync(Uuid tenantId, string? targetKind,
        Uuid? targetId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var take = Math.Clamp(limit, 1, 200);
        return targetKind is not null && targetId is { } id
            ? await WorkforceSourceSchema.Observations.QueryAsync(tx, WorkforceSourceSchema.ByTarget.Query()
                .WithPrefix(targetKind, id.ToString()).Take(take).After(cursor), ct).ConfigureAwait(false)
            : await WorkforceSourceSchema.Observations.QueryAsync(tx, WorkforceSourceSchema.ByObservedAt.Query()
                .Take(take).After(cursor), ct).ConfigureAwait(false);
    }
}
