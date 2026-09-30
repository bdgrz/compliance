using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

public sealed class RetireControlCriterionMappingHandler(IAggregateExecutor executor,
    TimeProvider clock)
    : IRequestHandler<RetireControlCriterionMapping>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RetireControlCriterionMapping> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return executor.ExecuteAsync(new ControlCriterionMappingLedger(request.TenantId,
                request.ProgramId),
            ledger => CommandFailureRequestAdapter.ToOutcome(ledger.Retire(request.MappingId,
                request.ExpectedRevision, request.Rationale,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct);
    }
}
