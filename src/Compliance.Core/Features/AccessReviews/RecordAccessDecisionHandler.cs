using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Records the assigned reviewer's own decision; HTTP-only, never an MCP tool.</summary>
public sealed class RecordAccessDecisionHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock, RestrictedApplicationVisibility visibility)
    : IRequestHandler<RecordAccessDecision, AccessDecisionView>
{
    public async ValueTask<Result<AccessDecisionView>> HandleAsync(
        IRequestContext<RecordAccessDecision> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = AccessReviewActor.From(context);
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(request.TenantId,
            request.CampaignId), ct).ConfigureAwait(false);
        if (!await RestrictedAccessReviewVisibility.CanReadCampaignItemsAsync(visibility,
                request.TenantId, actor.UserId, campaign, [request.ItemId], ct)
            .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessDecisionView>(RequestErrorKind.NotFound,
                "The review item was not found.");
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        var result = await executor.ExecuteAsync(new AccessReviewCampaign(request.TenantId,
                request.CampaignId),
            campaign => AccessReviewOutcome.From(campaign.Decide([request.ItemId],
                request.ExpectedRevision, context.RequestId, request.Decision, request.Rationale,
                actor.MemberId, actor.UserId, actor.Reference, clock.GetUtcNow(), null, waiver)),
            context, ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Result<AccessDecisionView>.Success(result.Value[0])
            : Result<AccessDecisionView>.Failure(result.Error);
    }
}
