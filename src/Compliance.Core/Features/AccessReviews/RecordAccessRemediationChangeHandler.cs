using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Records a provider-side change; it never counts as platform verification.</summary>
public sealed class RecordAccessRemediationChangeHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock, RestrictedApplicationVisibility visibility,
    AccessReviewQueueEligibility eligibility)
    : IRequestHandler<RecordAccessRemediationChange, AccessRemediationChangeView>
{
    public async ValueTask<Result<AccessRemediationChangeView>> HandleAsync(
        IRequestContext<RecordAccessRemediationChange> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = AccessReviewActor.From(context);
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(request.TenantId,
            request.CampaignId), ct).ConfigureAwait(false);
        if (!await RestrictedAccessReviewVisibility.CanReadCampaignItemsAsync(visibility,
                request.TenantId, actor.UserId, campaign, [request.ItemId], ct)
            .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessRemediationChangeView>(
                RequestErrorKind.NotFound, "The review item was not found.");
        if (campaign.FindItem(request.ItemId) is not { } item ||
            campaign.Launched?.ProgramId is { } programId &&
            (campaign.CurrentRemediationOwnerMemberId(request.ItemId) != actor.MemberId ||
             !await eligibility.CanRemediateAsync(request.TenantId, programId, actor.MemberId,
                 item.SystemInstanceId, ct).ConfigureAwait(false)))
            return AccessReviewOutcome.Failure<AccessRemediationChangeView>(RequestErrorKind.NotFound,
                "The review item was not found among the remediation owner's current queue access.");
        return await executor.ExecuteAsync(new AccessReviewCampaign(request.TenantId,
                request.CampaignId),
            campaign => AccessReviewOutcome.From(campaign.RecordChange(request.ItemId,
                request.ExpectedRevision, context.RequestId, request.Reference,
                request.Description, request.ChangedAt, actor.Reference, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
