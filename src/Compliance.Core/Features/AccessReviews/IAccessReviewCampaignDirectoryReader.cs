using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

public interface IAccessReviewCampaignDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);

    /// <summary>Lists campaigns, newest launch first.</summary>
    ValueTask<Page<AccessReviewCampaignSummaryView>> ListAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default);
}
