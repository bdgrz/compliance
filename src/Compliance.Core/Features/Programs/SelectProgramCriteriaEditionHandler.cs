using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class SelectProgramCriteriaEditionHandler(IAggregateExecutor executor,
    ICriteriaCatalog catalog, TimeProvider clock)
    : IRequestHandler<SelectProgramCriteriaEdition>
{
    public ValueTask<Result> HandleAsync(IRequestContext<SelectProgramCriteriaEdition> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var edition = catalog.GetEdition(request.EditionId);
        if (edition is null)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The criteria edition is not available.")));
        if (!edition.IsComplete)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The criteria edition is incomplete and cannot be selected.")));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException(
                "ProgramManagementAuthorizer must reject this actor.");
        return executor.ExecuteAsync(new ComplianceProgram(request.TenantId, request.ProgramId),
            program => CommandFailureRequestAdapter.ToOutcome(program.SelectCriteriaEdition(
                request.ExpectedRevision, request.EditionId,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct);
    }
}
