using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class RecordPartnerIndependenceEvaluationHandler(IAggregateExecutor executor,
    IAggregateReader reader, ProfessionalDutyAuthorityReader duties,
    CurrentRatifiedIndependenceRulesReader rulesReader, TimeProvider clock)
    : IRequestHandler<RecordPartnerIndependenceEvaluation, PartnerIndependenceEvaluationView>
{
    public async ValueTask<Result<PartnerIndependenceEvaluationView>> HandleAsync(
        IRequestContext<RecordPartnerIndependenceEvaluation> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            throw new InvalidOperationException("ProfessionalEngagementPartnerAuthorizer must reject this actor.");
        var currentDuty = await duties.ReadCurrentAsync(userId, FirmProfessionalDuty.EngagementPartner,
            context.Request.TenantId, ct).ConfigureAwait(false);
        if (currentDuty is null)
            return Refuse(RequestErrorKind.Forbidden,
                "The actor no longer has a current partner designation for this client.");

        var rules = await rulesReader.ReadActiveAsync(ct).ConfigureAwait(false);
        if (rules is null || rules.Version != context.Request.RuleVersion ||
            IndependenceSourceDigest.RuleContent(rules.Content) != context.Request.ExpectedRuleContentDigest)
            return Refuse(RequestErrorKind.Conflict,
                "The active ratified rules changed or are unavailable; reload the exact version and digest.", true);
        var ledger = await reader.HydrateAsync(new IndependenceLedger(context.Request.TenantId), ct)
            .ConfigureAwait(false);
        if (ledger.Sequence != context.Request.ExpectedSequence ||
            ledger.Engagement(context.Request.EngagementId) is not { Status: "draft" } engagement ||
            engagement.Revision != context.Request.ExpectedEngagementRevision ||
            IndependenceSourceDigest.Services(ledger.History().Services) != context.Request.ExpectedServiceHistoryDigest)
            return Refuse(RequestErrorKind.Conflict,
                "The draft or complete client service history changed; reload the current portfolio.", true);

        // Re-read authority and rules after collecting the source snapshot. These streams cannot share a
        // transaction with the client ledger, so the event also retains every exact revision and digest.
        var confirmedDuty = await duties.ReadCurrentAsync(userId, FirmProfessionalDuty.EngagementPartner,
            context.Request.TenantId, ct).ConfigureAwait(false);
        var confirmedRules = await rulesReader.ReadActiveAsync(ct).ConfigureAwait(false);
        if (confirmedDuty != currentDuty || confirmedRules is null ||
            confirmedRules.Version != rules.Version ||
            IndependenceSourceDigest.RuleContent(confirmedRules.Content) != context.Request.ExpectedRuleContentDigest)
            return Refuse(RequestErrorKind.Conflict,
                "Partner authority or ratified rules changed during review; reload before recording the evaluation.", true);

        var actor = ActorReference.ForFirmStaff(userId,
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        return await executor.ExecuteAsync(new IndependenceLedger(context.Request.TenantId), aggregate =>
            AggregateOutcome.CommitOnSuccess(aggregate.RecordPartnerIndependenceEvaluation(context.RequestId,
                context.Request, rules, currentDuty, actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }

    static Result<PartnerIndependenceEvaluationView> Refuse(RequestErrorKind kind, string message,
        bool transient = false) => Result<PartnerIndependenceEvaluationView>.Failure(
        new RequestError(kind, message, transient));
}
