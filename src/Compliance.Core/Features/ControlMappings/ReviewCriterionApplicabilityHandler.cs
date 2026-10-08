using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

public sealed class ReviewCriterionApplicabilityHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<ReviewCriterionApplicability>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<ReviewCriterionApplicability> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Criterion sign-off requires personal HTTP submission."));
        var request = context.Request;
        if (request.Outcome == "accept")
        {
            var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
                request.ProgramId), ct).ConfigureAwait(false);
            var ledger = await reader.HydrateAsync(new CriterionApplicabilityLedger(
                request.TenantId, request.ProgramId), ct).ConfigureAwait(false);
            if (ledger.Read(request.DecisionId) is { } decision &&
                program.CriteriaEditionId != decision.EditionId)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The decision targets an edition the program no longer selects."));
        }
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var memberId = RbacIds.Member(request.TenantId, userId);
        var actor = ActorReference.ForMember(memberId,
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        return await executor.ExecuteAsync(new CriterionApplicabilityLedger(request.TenantId,
                request.ProgramId),
            ledger => CommandFailureRequestAdapter.ToOutcome(ledger.Review(request.DecisionId,
                request.ExpectedRevision, context.RequestId, request.Outcome, request.Rationale,
                memberId, actor, clock.GetUtcNow(), waiver)),
            context, ct).ConfigureAwait(false);
    }
}
