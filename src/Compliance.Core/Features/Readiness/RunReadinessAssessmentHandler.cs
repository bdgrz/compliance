using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Reads the program's criteria edition selected at the requested time, mapping ledger, and
///     mapped controls, evaluates the current readiness rules as of that time, and records the
///     exact inputs and results.
///     A missing input is recorded as an acknowledged gap; it never blocks the run.
/// </summary>
public sealed class RunReadinessAssessmentHandler(IAggregateExecutor executor,
    IAggregateReader reader, ICriteriaCatalog catalog, IReadinessSourceReader sources,
    TimeProvider clock)
    : IRequestHandler<RunReadinessAssessment, ReadinessAssessmentRegistration>
{
    public async ValueTask<Result<ReadinessAssessmentRegistration>> HandleAsync(
        IRequestContext<RunReadinessAssessment> context, CancellationToken ct)
    {
        var request = context.Request;
        var now = clock.GetUtcNow();
        var asOf = request.AsOf ?? now;
        if (asOf > now)
            return Result<ReadinessAssessmentRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, "A readiness assessment cannot be as of a future time."));
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<ReadinessAssessmentRegistration>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The program was not found."));
        var selectedEditionId = program.CriteriaEditionAt(asOf);
        var catalogEdition = selectedEditionId is { } selected ? catalog.GetEdition(selected) : null;
        var editionId = catalogEdition is not null ? selectedEditionId : null;
        var criteria = editionId is { } edition
            ? catalog.ListEntries(edition, null, "criterion", null)
            : [];
        var ledger = await reader.HydrateAsync(new ControlCriterionMappingLedger(
            request.TenantId, request.ProgramId), ct).ConfigureAwait(false);
        var mappings = ledger.ReadAll().Where(mapping => mapping.EditionId == editionId)
            .ToArray();
        var controls = new Dictionary<Uuid, ControlVersionView?>();
        foreach (var controlId in mappings
                     .Where(mapping => ReadinessRules.ActiveAt(mapping, asOf) is not null)
                     .Select(static mapping => mapping.ControlId).Distinct())
        {
            var control = await reader.HydrateAsync(new ControlDraft(request.TenantId,
                controlId), ct).ConfigureAwait(false);
            controls[controlId] = control.IsVisible && control.ProgramId == request.ProgramId
                ? ReadinessControlSelection.EffectiveVersionAt(control, asOf)
                : null;
        }
        var sourceResult = await sources.ReadAsync(request.TenantId, request.ProgramId, asOf, ct)
            .ConfigureAwait(false);
        if (!sourceResult.IsSuccess)
            return Result<ReadinessAssessmentRegistration>.Failure(sourceResult.Error);
        var evaluation = ReadinessRules.Evaluate(request.ProgramId, asOf, editionId, criteria,
            mappings, controls, sourceResult.Value, catalogEdition);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return await executor.ExecuteAsync(new ReadinessLedger(request.TenantId,
                request.ProgramId),
            readiness =>
            {
                var failure = readiness.Record(request.ExpectedRevision, context.RequestId, asOf,
                    editionId, evaluation, RbacIds.Member(request.TenantId, userId),
                    UserIdentityClaims.BdgrzDisplay(context.Actor, userId), now);
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    new ReadinessAssessmentRegistration(context.RequestId, readiness.Revision));
            }, context, ct).ConfigureAwait(false);
    }
}
