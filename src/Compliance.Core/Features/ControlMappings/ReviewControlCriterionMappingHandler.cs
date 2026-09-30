using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

public sealed class ReviewControlCriterionMappingHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<ReviewControlCriterionMapping>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<ReviewControlCriterionMapping> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Outcome == "accept")
        {
            var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
                request.ProgramId), ct).ConfigureAwait(false);
            var ledger = await reader.HydrateAsync(new ControlCriterionMappingLedger(
                request.TenantId, request.ProgramId), ct).ConfigureAwait(false);
            if (ledger.EditionOf(request.MappingId) is { } edition &&
                ledger.HasPendingProposal(request.MappingId) &&
                program.CriteriaEditionId != edition)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The mapping targets an edition the program no longer selects; remap it explicitly."));
        }
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return await executor.ExecuteAsync(new ControlCriterionMappingLedger(request.TenantId,
                request.ProgramId),
            ledger => CommandFailureRequestAdapter.ToOutcome(ledger.Review(request.MappingId,
                request.ExpectedRevision, context.RequestId, request.Outcome, request.Rationale,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow(),
                waiver)),
            context, ct).ConfigureAwait(false);
    }
}
