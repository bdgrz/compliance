using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

public interface IAccessPopulationDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);

    /// <summary>Lists one system instance's populations, newest observation first.</summary>
    ValueTask<Page<AccessPopulationSummaryView>> ListAsync(Uuid tenantId, Uuid systemInstanceId,
        int limit, string? cursor, CancellationToken ct = default);
}
