using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Returns the whole campaign to access-review managers and only their own items to assigned
///     reviewers. Anyone else receives not found.
/// </summary>
public sealed class GetAccessReviewCampaignHandler(IAggregateReader reader,
    IPermissionAuthorizer permissions, TimeProvider clock)
    : IRequestHandler<GetAccessReviewCampaign, AccessReviewCampaignView>
{
    public async ValueTask<Result<AccessReviewCampaignView>> HandleAsync(
        IRequestContext<GetAccessReviewCampaign> context, CancellationToken ct)
    {
        var request = context.Request;
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(request.TenantId,
            request.CampaignId), ct).ConfigureAwait(false);
        if (!campaign.IsLaunched)
            return NotFound();
        var actor = AccessReviewActor.From(context);
        if (await permissions.IsAllowedAsync(request.TenantId, actor.UserId, actor.MemberId,
                RbacPermissions.AccessReviewManage, ct).ConfigureAwait(false))
            return Result<AccessReviewCampaignView>.Success(campaign.ToView(clock.GetUtcNow()));
        return campaign.IsReviewer(actor.MemberId)
            ? Result<AccessReviewCampaignView>.Success(campaign.ToView(clock.GetUtcNow(),
                item => item.ReviewerMemberId == actor.MemberId))
            : NotFound();
    }

    static Result<AccessReviewCampaignView> NotFound() =>
        AccessReviewOutcome.Failure<AccessReviewCampaignView>(RequestErrorKind.NotFound,
            "The campaign was not found.");
}
