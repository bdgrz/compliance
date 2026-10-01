using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Records a provider-side change; it never counts as platform verification.</summary>
public sealed class RecordAccessRemediationChangeHandler(IAggregateExecutor executor,
    TimeProvider clock)
    : IRequestHandler<RecordAccessRemediationChange, AccessRemediationChangeView>
{
    public async ValueTask<Result<AccessRemediationChangeView>> HandleAsync(
        IRequestContext<RecordAccessRemediationChange> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = AccessReviewActor.From(context);
        return await executor.ExecuteAsync(new AccessReviewCampaign(request.TenantId,
                request.CampaignId),
            campaign => AccessReviewOutcome.From(campaign.RecordChange(request.ItemId,
                request.ExpectedRevision, context.RequestId, request.Reference,
                request.Description, request.ChangedAt, actor.Reference, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
