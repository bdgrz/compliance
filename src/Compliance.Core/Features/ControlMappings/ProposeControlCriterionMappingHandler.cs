using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

public sealed class ProposeControlCriterionMappingHandler(IAggregateExecutor executor,
    IAggregateReader reader, ICriteriaCatalog catalog, ControlActivationSource controls,
    TimeProvider clock)
    : IRequestHandler<ProposeControlCriterionMapping, ControlCriterionMappingRegistration>
{
    public async ValueTask<Result<ControlCriterionMappingRegistration>> HandleAsync(
        IRequestContext<ProposeControlCriterionMapping> context, CancellationToken ct)
    {
        var request = context.Request;
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Failure(RequestErrorKind.NotFound, "The program was not found.");
        if (program.CriteriaEditionId != request.EditionId)
            return Failure(RequestErrorKind.Conflict,
                "A mapping must target the criteria edition the program has selected.");
        var entry = catalog.GetEntry(request.EditionId, request.CriterionIdentifier ?? "");
        if (entry is null)
            return Failure(RequestErrorKind.Validation,
                "The criterion or point of focus is not in the selected edition.");
        var control = await controls.LoadAsync(request.TenantId, request.ProgramId,
            request.ControlId, ct).ConfigureAwait(false);
        if (!control.IsSuccess)
            return Result<ControlCriterionMappingRegistration>.Failure(control.Error);
        if (control.Value.ApprovedVersion?.VersionId != request.ControlVersionId)
            return Failure(RequestErrorKind.Conflict,
                "A mapping must reference an approved version of the control.");
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return await executor.ExecuteAsync(new ControlCriterionMappingLedger(request.TenantId,
                request.ProgramId),
            ledger =>
            {
                var failure = ledger.Propose(request.ControlId, request.ControlVersionId,
                    request.EditionId, entry.Identifier, entry.Kind, request.ExpectedRevision,
                    request.Rationale, request.ApplicabilityExplanation,
                    RbacIds.Member(request.TenantId, userId),
                    UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow(),
                    out var registration);
                return CommandFailureRequestAdapter.ToOutcome(failure, registration!);
            }, context, ct).ConfigureAwait(false);
    }

    static Result<ControlCriterionMappingRegistration> Failure(RequestErrorKind kind,
        string message) => Result<ControlCriterionMappingRegistration>.Failure(
        new RequestError(kind, message));
}
