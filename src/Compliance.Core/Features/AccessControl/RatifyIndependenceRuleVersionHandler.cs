using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class RatifyIndependenceRuleVersionHandler(IAggregateExecutor executor,
    IAggregateReader reader, ProfessionalDutyAuthorityReader duties, TimeProvider clock)
    : IRequestHandler<RatifyIndependenceRuleVersion, IndependenceRuleRatificationView>
{
    public async ValueTask<Result<IndependenceRuleRatificationView>> HandleAsync(
        IRequestContext<RatifyIndependenceRuleVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            throw new InvalidOperationException("IndependenceRuleRatificationAuthorizer must reject this actor.");
        var currentDuty = await duties.ReadCurrentAsync(userId, FirmProfessionalDuty.RuleRatifier, null, ct)
            .ConfigureAwait(false);
        if (currentDuty is null)
            return Refuse(RequestErrorKind.Forbidden,
                "The actor no longer has a current rule-ratifier designation.");

        var rules = await reader.HydrateAsync(new IndependenceRuleCatalog(), ct).ConfigureAwait(false);
        if (rules.Sequence != request.ExpectedRuleCatalogSequence || rules.Current is not { } version ||
            version.Version != request.RuleVersion ||
            IndependenceSourceDigest.RuleContent(version.Content) != request.ExpectedContentDigest)
            return Refuse(RequestErrorKind.Conflict,
                "The exact current draft changed; reload its immutable version and digest before ratifying.", true);

        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out userId))
            throw new InvalidOperationException("IndependenceRuleRatificationAuthorizer must reject this actor.");
        var actor = ActorReference.ForFirmStaff(userId, UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        var ratifications = await reader.HydrateAsync(new IndependenceRuleRatificationCatalog(), ct)
            .ConfigureAwait(false);
        if (ratifications.Sequence != request.ExpectedRatificationSequence ||
            request.RuleVersion <= (ratifications.Active?.RuleVersion ?? 0))
            return Refuse(RequestErrorKind.Conflict,
                "The ratification sequence changed or a newer version is already active; reload before ratifying.", true);
        return await executor.ExecuteAsync(new IndependenceRuleRatificationCatalog(), catalog =>
            AggregateOutcome.CommitOnSuccess(catalog.Ratify(context.RequestId, request.RatificationId,
                request.RuleVersion, request.ExpectedRuleCatalogSequence, request.ExpectedRatificationSequence,
                request.ExpectedContentDigest, request.SourceReference, currentDuty.Staff,
                currentDuty.Designation, actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }

    static Result<IndependenceRuleRatificationView> Refuse(RequestErrorKind kind, string message,
        bool transient = false) => Result<IndependenceRuleRatificationView>.Failure(
        new RequestError(kind, message, transient));
}
