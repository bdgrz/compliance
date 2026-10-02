using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Reads which program evidence requests are fulfilled right now.</summary>
static class RiskActionEvidence
{
    public static async ValueTask<IReadOnlySet<Uuid>> FulfilledAsync(IAggregateReader reader,
        Uuid tenantId, Uuid programId, CancellationToken ct)
    {
        var evidence = await reader.HydrateAsync(new EvidenceRequestLedger(tenantId, programId),
            ct).ConfigureAwait(false);
        return evidence.ReadAll()
            .Where(static request => request.Status == EvidenceRequestLedger.Fulfilled)
            .Select(static request => request.EvidenceRequestId).ToHashSet();
    }
}
