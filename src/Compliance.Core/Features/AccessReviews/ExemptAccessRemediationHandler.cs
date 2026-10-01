using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Records the acting member's personal remediation exception approval; HTTP-only.</summary>
public sealed class ExemptAccessRemediationHandler(IAggregateExecutor executor,
    TimeProvider clock)
    : IRequestHandler<ExemptAccessRemediation, AccessRemediationExceptionView>
{
    public async ValueTask<Result<AccessRemediationExceptionView>> HandleAsync(
        IRequestContext<ExemptAccessRemediation> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = AccessReviewActor.From(context);
        return await executor.ExecuteAsync(new AccessReviewCampaign(request.TenantId,
                request.CampaignId),
            campaign => AccessReviewOutcome.From(campaign.RecordException(request.ItemId,
                request.ExpectedRevision, context.RequestId, request.Rationale, request.ExpiresAt,
                actor.MemberId, actor.Reference, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }
}
