using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public interface IRiskDraftHistoryDirectoryReader
{
    ValueTask<RiskDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid riskId,
        long revision, CancellationToken ct = default);
    ValueTask<Page<RiskDraftRevisionView>> ListRevisionsAsync(Uuid tenantId, Uuid riskId,
        int limit, string? cursor, CancellationToken ct = default);
}
