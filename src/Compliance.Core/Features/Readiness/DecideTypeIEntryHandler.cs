using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Records the acting member's own Type I entry sign-off; HTTP-only, never an MCP tool.</summary>
public sealed class DecideTypeIEntryHandler(IAggregateExecutor executor, IAggregateReader reader,
    TimeProvider clock) : IRequestHandler<DecideTypeIEntry, TypeIEntryDecisionView>
{
    public async ValueTask<Result<TypeIEntryDecisionView>> HandleAsync(
        IRequestContext<DecideTypeIEntry> context, CancellationToken ct)
    {
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
                var failure = ledger.DecideTypeIEntry(request.AssessmentId,
                    request.ExpectedRevision, context.RequestId, request.Outcome,
                    request.Rationale, request.AcknowledgedGapIds ?? [],
                    RbacIds.Member(request.TenantId, userId),
                    UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow(),
                    waiver);
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    ledger.FindTypeIEntryDecision(context.RequestId)!);
            }, context, ct).ConfigureAwait(false);
    }
}
