using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Records the acting member's own readiness decision; HTTP-only, never an MCP tool.</summary>
public sealed class DecideReadinessHandler(IAggregateExecutor executor, IAggregateReader reader,
    TimeProvider clock) : IRequestHandler<DecideReadiness, ReadinessDecisionView>
{
    public async ValueTask<Result<ReadinessDecisionView>> HandleAsync(
        IRequestContext<DecideReadiness> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result<ReadinessDecisionView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Readiness sign-off requires a personal HTTP invocation."));
        var request = context.Request;
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return await executor.ExecuteAsync(new ReadinessLedger(request.TenantId,
                request.ProgramId),
            ledger =>
            {
                var failure = ledger.Decide(request.AssessmentId, request.ExpectedRevision,
                    context.RequestId, request.Outcome, request.Rationale,
                    RbacIds.Member(request.TenantId, userId),
                    UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow(),
                    waiver);
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    ledger.FindDecision(request.AssessmentId)!);
            }, context, ct).ConfigureAwait(false);
    }
}
