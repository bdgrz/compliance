using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Lists from the Fitz campaign directory once it has reached the source.</summary>
public sealed class ListAccessReviewCampaignsHandler(IAccessReviewCampaignDirectoryReader directory,
    IDomainEventReader events)
    : IRequestHandler<ListAccessReviewCampaigns, Page<AccessReviewCampaignSummaryView>>
{
    public async ValueTask<Result<Page<AccessReviewCampaignSummaryView>>> HandleAsync(
        IRequestContext<ListAccessReviewCampaigns> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return AccessReviewOutcome.Failure<Page<AccessReviewCampaignSummaryView>>(
                RequestErrorKind.Validation, "The campaign list limit must be between 1 and 200.");
        var checkpoint = await directory.LoadCheckpointAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!await AccessReviewOutcome.IsCaughtUpAsync(events,
                AccessReviewDirectorySchema.CampaignPattern(request.TenantId), checkpoint, ct)
                .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<Page<AccessReviewCampaignSummaryView>>(
                RequestErrorKind.Conflict, "The campaign directory has not reached the source.", true);
        Page<AccessReviewCampaignSummaryView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Limit ?? 50, request.Cursor, ct)
                .ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return AccessReviewOutcome.Failure<Page<AccessReviewCampaignSummaryView>>(
                RequestErrorKind.Validation, "The campaign cursor is invalid.");
        }
        return page.Items.Any(item => item.TenantId != request.TenantId)
            ? AccessReviewOutcome.Failure<Page<AccessReviewCampaignSummaryView>>(
                RequestErrorKind.NotFound, "The campaigns were not found.")
            : Result<Page<AccessReviewCampaignSummaryView>>.Success(page);
    }
}
