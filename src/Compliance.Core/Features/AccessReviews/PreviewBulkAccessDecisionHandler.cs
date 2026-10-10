using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications;

namespace Bdgrz.Compliance.Features.AccessReviews;

public sealed class PreviewBulkAccessDecisionHandler(IAggregateReader reader,
    RestrictedApplicationVisibility visibility, AccessReviewQueueEligibility eligibility)
    : IRequestHandler<PreviewBulkAccessDecision, BulkAccessDecisionPreview>
{
    public async ValueTask<Result<BulkAccessDecisionPreview>> HandleAsync(
        IRequestContext<PreviewBulkAccessDecision> context, CancellationToken ct)
    {
        var request = context.Request;
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(request.TenantId,
            request.CampaignId), ct).ConfigureAwait(false);
        var actor = AccessReviewActor.From(context);
        if (campaign.Launched is not { } launched)
            return AccessReviewOutcome.Failure<BulkAccessDecisionPreview>(RequestErrorKind.NotFound,
                "The campaign was not found.");
        foreach (var itemId in request.ItemIds.Distinct())
        {
            var item = launched.Items.FirstOrDefault(candidate => candidate.ItemId == itemId);
            if (item is null || !await visibility.CanReadSystemInstanceAsync(request.TenantId,
                    actor.UserId, item.SystemInstanceId, ct).ConfigureAwait(false) ||
                campaign.CurrentReviewerMemberId(itemId) != actor.MemberId ||
                launched.ProgramId is { } programId &&
                !await eligibility.CanReviewAsync(request.TenantId, programId, actor.MemberId,
                    item.SystemInstanceId, ct).ConfigureAwait(false))
                return AccessReviewOutcome.Failure<BulkAccessDecisionPreview>(
                    RequestErrorKind.NotFound, "The campaign was not found.");
        }
        return BulkAccessDecisions.Preview(campaign, request.ItemIds, request.Decision,
            actor.MemberId, actor.UserId);
    }
}
