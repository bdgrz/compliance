using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Records one shared-rationale decision only when the request names exactly the previewed
///     eligible items and presents the preview token for the current campaign revision. HTTP-only.
/// </summary>
public sealed class RecordBulkAccessDecisionHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock, RestrictedApplicationVisibility visibility)
    : IRequestHandler<RecordBulkAccessDecision, BulkAccessDecisionResult>
{
    public async ValueTask<Result<BulkAccessDecisionResult>> HandleAsync(
        IRequestContext<RecordBulkAccessDecision> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return AccessReviewOutcome.Failure<BulkAccessDecisionResult>(RequestErrorKind.Forbidden,
                "Access review sign-off requires personal HTTP submission.");
        var request = context.Request;
        var itemIds = request.ItemIds ?? [];
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(request.TenantId,
            request.CampaignId), ct).ConfigureAwait(false);
        var actor = AccessReviewActor.From(context);
        if (!await RestrictedAccessReviewVisibility.CanReadCampaignItemsAsync(visibility,
                request.TenantId, actor.UserId, campaign, itemIds, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<BulkAccessDecisionResult>(RequestErrorKind.NotFound,
                "The review items were not found.");
        var result = await executor.ExecuteAsync(new AccessReviewCampaign(request.TenantId,
                request.CampaignId),
            campaign =>
            {
                if (!campaign.HasDecision(context.RequestId))
                {
                    var preview = BulkAccessDecisions.Preview(campaign, itemIds, request.Decision,
                        actor.MemberId, actor.UserId);
                    if (!preview.IsSuccess)
                        return AccessReviewOutcome.From(
                            Result<IReadOnlyList<AccessDecisionView>>.Failure(preview.Error));
                    if (preview.Value.Rejected.Count > 0 ||
                        preview.Value.Eligible.Count != itemIds.Count ||
                        !string.Equals(preview.Value.PreviewToken, request.PreviewToken,
                            StringComparison.Ordinal))
                        return AccessReviewOutcome.From(AccessReviewOutcome
                            .Failure<IReadOnlyList<AccessDecisionView>>(RequestErrorKind.Conflict,
                                "The bulk decision no longer matches its preview; preview it again."));
                }
                return AccessReviewOutcome.From(campaign.Decide(itemIds, campaign.Revision,
                    context.RequestId, request.Decision, request.Rationale, actor.MemberId,
                    actor.UserId, actor.Reference, clock.GetUtcNow(), request.PreviewToken, null));
            }, context, ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Result<BulkAccessDecisionResult>.Success(new BulkAccessDecisionResult(result.Value))
            : Result<BulkAccessDecisionResult>.Failure(result.Error);
    }
}
