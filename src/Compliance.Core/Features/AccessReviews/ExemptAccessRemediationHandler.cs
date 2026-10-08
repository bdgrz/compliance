using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Records the acting member's personal remediation exception approval; HTTP-only.</summary>
public sealed class ExemptAccessRemediationHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock, RestrictedApplicationVisibility visibility)
    : IRequestHandler<ExemptAccessRemediation, AccessRemediationExceptionView>
{
    public async ValueTask<Result<AccessRemediationExceptionView>> HandleAsync(
        IRequestContext<ExemptAccessRemediation> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return AccessReviewOutcome.Failure<AccessRemediationExceptionView>(RequestErrorKind.Forbidden,
                "Access review sign-off requires personal HTTP submission.");
        var request = context.Request;
        var actor = AccessReviewActor.From(context);
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(request.TenantId,
            request.CampaignId), ct).ConfigureAwait(false);
        if (!await RestrictedAccessReviewVisibility.CanReadCampaignItemsAsync(visibility,
                request.TenantId, actor.UserId, campaign, [request.ItemId], ct)
            .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessRemediationExceptionView>(
                RequestErrorKind.NotFound, "The review item was not found.");
        return await executor.ExecuteAsync(new AccessReviewCampaign(request.TenantId,
                request.CampaignId),
            campaign => AccessReviewOutcome.From(campaign.RecordException(request.ItemId,
                request.ExpectedRevision, context.RequestId, request.Rationale, request.ExpiresAt,
                actor.MemberId, actor.Reference, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }
}
