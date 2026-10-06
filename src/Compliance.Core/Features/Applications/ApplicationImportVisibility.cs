using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Resolves governed application state from durable source commit proofs.</summary>
static class ApplicationImportVisibility
{
    public static async ValueTask<DeclaredApplication> HydrateApplicationAsync(this IAggregateReader reader,
        Uuid tenantId, Uuid applicationId, CancellationToken ct = default)
    {
        var application = await reader.HydrateAsync(new DeclaredApplication(tenantId, applicationId), ct)
            .ConfigureAwait(false);
        foreach (var source in application.GetPendingImportEffects().Select(effect =>
                     (effect.Plan.SourceKey, effect.Plan.SourceNamespace)).Distinct())
        {
            var ledger = await reader.HydrateAsync(new ApplicationImportLedger(tenantId,
                source.SourceKey, source.SourceNamespace), ct).ConfigureAwait(false);
            application.ResolveImportVisibility(ledger);
        }
        return application;
    }
}
