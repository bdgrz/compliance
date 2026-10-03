using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public interface IAssuranceReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default);
    ValueTask<AssuranceReportView?> GetReportRevisionAsync(Uuid tenantId, Uuid reportId,
        long revision, CancellationToken ct = default);
    ValueTask<Page<AssuranceReportView>> ListReportsAsync(Uuid tenantId, Uuid providerId, int limit, string? cursor, CancellationToken ct = default);
    ValueTask<Page<ProviderReviewView>> ListReviewsAsync(Uuid tenantId, Uuid providerId, int limit, string? cursor, CancellationToken ct = default);
}
