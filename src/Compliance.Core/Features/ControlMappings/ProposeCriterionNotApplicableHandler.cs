using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>Proposes that one criterion of the program's selected edition does not apply.</summary>
public sealed class ProposeCriterionNotApplicableHandler(IAggregateExecutor executor,
    IAggregateReader reader, ICriteriaCatalog catalog, TimeProvider clock)
    : IRequestHandler<ProposeCriterionNotApplicable, CriterionApplicabilityRegistration>
{
    public async ValueTask<Result<CriterionApplicabilityRegistration>> HandleAsync(
        IRequestContext<ProposeCriterionNotApplicable> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result<CriterionApplicabilityRegistration>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Criterion sign-off requires personal HTTP submission."));
        var request = context.Request;
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Failure(RequestErrorKind.NotFound, "The program was not found.");
        if (program.CriteriaEditionId != request.EditionId)
            return Failure(RequestErrorKind.Conflict,
                "A not-applicable decision must target the criteria edition the program has selected.");
        if (catalog.GetEntry(request.EditionId, request.CriterionIdentifier ?? "") is not
            { Kind: "criterion" } entry)
            return Failure(RequestErrorKind.Validation,
                "Not-applicable decisions apply to a criterion of the selected edition, not a point of focus.");
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var memberId = RbacIds.Member(request.TenantId, userId);
        var actor = ActorReference.ForMember(memberId,
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        return await executor.ExecuteAsync(new CriterionApplicabilityLedger(request.TenantId,
                request.ProgramId),
            ledger =>
            {
                var failure = ledger.Propose(request.EditionId, entry.Identifier,
                    request.ExpectedRevision, request.Rationale, memberId, actor,
                    clock.GetUtcNow(), out var registration);
                return CommandFailureRequestAdapter.ToOutcome(failure, registration!);
            }, context, ct).ConfigureAwait(false);
    }

    static Result<CriterionApplicabilityRegistration> Failure(RequestErrorKind kind,
        string message) => Result<CriterionApplicabilityRegistration>.Failure(
        new RequestError(kind, message));
}
