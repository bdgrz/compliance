using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Records a finding. A governed source must exist in this program: a readiness gap names its
///     assessment as the source version, and a control occurrence must have been recorded.
/// </summary>
public sealed class RaiseFindingHandler(IAggregateExecutor executor, IAggregateReader reader,
    TimeProvider clock) : IRequestHandler<RaiseFinding, FindingRegistration>
{
    public async ValueTask<Result<FindingRegistration>> HandleAsync(
        IRequestContext<RaiseFinding> context, CancellationToken ct)
    {
        var request = context.Request;
        if (await SourceErrorAsync(request, ct).ConfigureAwait(false) is { } sourceError)
            return Result<FindingRegistration>.Failure(sourceError);
        if (!await RemediationCommands.IsActiveMemberAsync(reader, request.TenantId,
                request.OwnerMemberId, ct).ConfigureAwait(false))
            return Result<FindingRegistration>.Failure(RemediationCommands.InactiveOwner());
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        return await executor.ExecuteAsync(new RemediationLedger(request.TenantId,
                request.ProgramId),
            ledger =>
            {
                var failure = ledger.Raise(context.RequestId, request.Source, request.Title,
                    request.Description, request.Severity, request.AffectedScope,
                    request.OwnerMemberId, request.DueOn, request.Links ?? [],
                    ActorReference.ForMember(actor.MemberId, actor.Display), clock.GetUtcNow());
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    new FindingRegistration(context.RequestId, 1));
            }, context, ct).ConfigureAwait(false);
    }

    async ValueTask<RequestError?> SourceErrorAsync(RaiseFinding request, CancellationToken ct)
    {
        var source = request.Source;
        if (source?.RecordId is not { } recordId)
            return null;
        var missing = new RequestError(RequestErrorKind.Validation,
            "The finding source record was not found in this program.");
        switch (source.Kind)
        {
            case "readiness_gap":
                if (!Uuid.TryParse(source.Version, null, out var assessmentId))
                    return missing;
                var readiness = await reader.HydrateAsync(new ReadinessLedger(request.TenantId,
                    request.ProgramId), ct).ConfigureAwait(false);
                return readiness.ReadGaps(assessmentId)?.Any(gap => gap.GapId == recordId) == true
                    ? null
                    : missing;
            case "control_occurrence" or "occurrence_review":
                var operations = await reader.HydrateAsync(new ControlOperationsLedger(
                    request.TenantId, request.ProgramId), ct).ConfigureAwait(false);
                return operations.OccurrenceControlId(recordId) is null ? missing : null;
            default:
                return null;
        }
    }
}
