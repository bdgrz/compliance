using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class GetServiceEngagementPartnerReviewContextHandler(IAggregateReader reader,
    ProfessionalDutyAuthorityReader duties, CurrentRatifiedIndependenceRulesReader rulesReader)
    : IRequestHandler<GetServiceEngagementPartnerReviewContext, ServiceEngagementPartnerReviewContextView>
{
    public async ValueTask<Result<ServiceEngagementPartnerReviewContextView>> HandleAsync(
        IRequestContext<GetServiceEngagementPartnerReviewContext> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            throw new InvalidOperationException("ProfessionalEngagementPartnerAuthorizer must reject this actor.");
        var duty = await duties.ReadCurrentAsync(userId, FirmProfessionalDuty.EngagementPartner,
            context.Request.TenantId, ct).ConfigureAwait(false);
        if (duty is null)
            return Result<ServiceEngagementPartnerReviewContextView>.Failure(new RequestError(
                RequestErrorKind.Forbidden,
                "The actor no longer has a current partner designation for this client."));

        var ledger = await reader.HydrateAsync(new IndependenceLedger(context.Request.TenantId), ct)
            .ConfigureAwait(false);
        if (ledger.Engagement(context.Request.EngagementId) is not { } target)
            return Result<ServiceEngagementPartnerReviewContextView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The client engagement was not found."));
        var rules = await rulesReader.ReadActiveAsync(ct).ConfigureAwait(false);
        var currentLedger = await reader.HydrateAsync(new IndependenceLedger(context.Request.TenantId), ct)
            .ConfigureAwait(false);
        var currentRules = await rulesReader.ReadActiveAsync(ct).ConfigureAwait(false);
        var currentDuty = await duties.ReadCurrentAsync(userId, FirmProfessionalDuty.EngagementPartner,
            context.Request.TenantId, ct).ConfigureAwait(false);
        if (currentDuty != duty || currentLedger.Sequence != ledger.Sequence ||
            !SameRules(currentRules, rules))
            return Result<ServiceEngagementPartnerReviewContextView>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "Partner authority, client facts or ratified rules changed during review; reload the current context.", true));

        var view = new ServiceEngagementPartnerReviewContextView(context.Request.TenantId, ledger.Sequence,
            target, ledger.Engagements, ledger.AllAcceptanceHistory, ledger.ClientActualAssignmentHistory,
            ledger.History().Services, ledger.AllManagementAcknowledgements,
            ledger.ManagementAcknowledgements(target.EngagementId), rules,
            ledger.History().PartnerEvaluations);
        return Result<ServiceEngagementPartnerReviewContextView>.Success(view);
    }

    static bool SameRules(IndependenceRuleVersionView? current, IndependenceRuleVersionView? captured) =>
        current is null && captured is null || current is not null && captured is not null &&
        current.Version == captured.Version && current.IsRatified && captured.IsRatified &&
        IndependenceSourceDigest.RuleContent(current.Content) ==
            IndependenceSourceDigest.RuleContent(captured.Content) &&
        current.Ratification?.RatificationId == captured.Ratification?.RatificationId;
}
