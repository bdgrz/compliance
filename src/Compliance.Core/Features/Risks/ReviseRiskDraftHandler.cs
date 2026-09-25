using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class ReviseRiskDraftHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ReviseRiskDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseRiskDraft> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        RiskDraftContent content = new(request.Title, request.Scenario,
            request.PotentialEffect, request.SourceNote);
        return executor.ExecuteAsync(new RiskDraft(request.TenantId, request.RiskId),
            risk => AggregateOutcome.CommitOnSuccess(risk.Revise(request.ProgramId,
                request.ExpectedRevision, content,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct);
    }
}
