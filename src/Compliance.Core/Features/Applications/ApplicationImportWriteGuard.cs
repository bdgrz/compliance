using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Captures durable import settlement for a guarded application-stream execution.</summary>
static class ApplicationImportWriteGuard
{
    public static async ValueTask<DeclaredApplication> PrepareAsync(IAggregateReader reader,
        Uuid tenantId, Uuid applicationId, CancellationToken ct)
    {
        var source = await reader.HydrateAsync(new DeclaredApplication(tenantId, applicationId), ct)
            .ConfigureAwait(false);
        var settled = new HashSet<Uuid>();
        var committed = new HashSet<Uuid>();
        foreach (var group in source.GetPendingImportEffects().GroupBy(ev => (ev.Plan.SourceKey, ev.Plan.SourceNamespace)))
        {
            var ledger = await reader.HydrateAsync(new ApplicationImportLedger(tenantId,
                group.Key.SourceKey, group.Key.SourceNamespace), ct).ConfigureAwait(false);
            foreach (var batchId in group.Select(ev => ev.Plan.BatchId).Distinct())
                if (ledger.IsRollbackDurable(batchId))
                    settled.Add(batchId);
            foreach (var effect in group)
                if (ledger.IsEffectCommitted(effect))
                {
                    settled.Add(effect.Plan.BatchId);
                    committed.Add(effect.Metadata.EventId);
                }
        }
        return new DeclaredApplication(tenantId, applicationId, settled, committed);
    }
}
