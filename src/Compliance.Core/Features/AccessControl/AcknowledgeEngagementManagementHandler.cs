using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Records the owning client's personal management decision; never a professional approval.</summary>
public sealed class AcknowledgeEngagementManagementHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<AcknowledgeEngagementManagement, EngagementManagementAcknowledgementView>
{
    public ValueTask<Result<EngagementManagementAcknowledgementView>> HandleAsync(
        IRequestContext<AcknowledgeEngagementManagement> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return ValueTask.FromResult(Result<EngagementManagementAcknowledgementView>.Failure(new RequestError(
                RequestErrorKind.Forbidden, "Management responsibility requires the authenticated client's personal HTTP acknowledgement.")));
        return executor.ExecuteAsync(new IndependenceLedger(context.Request.TenantId), ledger =>
            AggregateOutcome.CommitOnSuccess(ledger.AcknowledgeManagement(context.RequestId,
                context.Request, userId, AccessGrantActor.From(context, context.Request.TenantId), clock.GetUtcNow())), context, ct);
    }
}
