using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public interface IRiskEvaluationDirectoryReader
{
    ValueTask<RiskEvaluationView?> GetAsync(Uuid tenantId, Uuid riskId,
        CancellationToken ct = default);
    ValueTask<Page<RiskEvaluationHistoryEntryView>> ListHistoryAsync(Uuid tenantId, Uuid riskId,
        int limit, string? cursor, CancellationToken ct = default);
}
