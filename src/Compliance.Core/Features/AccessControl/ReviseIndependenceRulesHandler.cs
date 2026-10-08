using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ReviseIndependenceRulesHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviseIndependenceRules, IndependenceRuleVersionView>
{
    public ValueTask<Result<IndependenceRuleVersionView>> HandleAsync(
        IRequestContext<ReviseIndependenceRules> context, CancellationToken ct)
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("IndependenceRuleAdministrationAuthorizer must reject this actor.");
        var actor = ActorReference.ForPlatformOperator(userId, UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        return executor.ExecuteAsync(new IndependenceRuleCatalog(), catalog =>
            AggregateOutcome.CommitOnSuccess(catalog.ReviseRules(context.RequestId,
                context.Request.ExpectedSequence, context.Request.Content, actor, clock.GetUtcNow())), context, ct);
    }
}
