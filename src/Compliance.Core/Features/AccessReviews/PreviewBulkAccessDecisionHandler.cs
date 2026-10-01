using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

public sealed class PreviewBulkAccessDecisionHandler(IAggregateReader reader)
    : IRequestHandler<PreviewBulkAccessDecision, BulkAccessDecisionPreview>
{
    public async ValueTask<Result<BulkAccessDecisionPreview>> HandleAsync(
        IRequestContext<PreviewBulkAccessDecision> context, CancellationToken ct)
    {
        var request = context.Request;
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(request.TenantId,
            request.CampaignId), ct).ConfigureAwait(false);
        var actor = AccessReviewActor.From(context);
        return BulkAccessDecisions.Preview(campaign, request.ItemIds, request.Decision,
            actor.MemberId, actor.UserId);
    }
}
