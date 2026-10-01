using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Records the reviewer's own independent decision; HTTP-only. Only the plan's reviewer or a
///     program manager may review, and never the performer or recorder without a waiver.
/// </summary>
public sealed class ReviewControlOccurrenceHandler(IAggregateExecutor executor,
    IAggregateReader reader, OperatingAuthority authority, TimeProvider clock)
    : IRequestHandler<ReviewControlOccurrence, ControlOccurrenceView>
{
    public async ValueTask<Result<ControlOccurrenceView>> HandleAsync(
        IRequestContext<ReviewControlOccurrence> context, CancellationToken ct)
    {
        var request = context.Request;
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var control = await ControlOperationsSource.LoadControlAsync(reader, request.TenantId,
            request.ProgramId, request.ControlId, ct).ConfigureAwait(false);
        if (control is null)
            return Result<ControlOccurrenceView>.Failure(ControlOperationsSource.ControlNotFound());
        var windows = OperatingAuthority.VersionWindows(control);
        var current = await reader.HydrateAsync(new ControlOperationsLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (current.ReadOccurrence(request.ControlId, request.OccurrenceId, windows, today) is not
            { } occurrence || current.FindPlan(request.ControlId, occurrence.PlanVersionId) is
                not { } plan)
            return Result<ControlOccurrenceView>.Failure(ControlOperationsSource.OccurrenceNotFound());
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var reviewer = (current.CurrentPlan(request.ControlId) ?? plan).ReviewerMemberId;
        if (actor.MemberId != reviewer && !await authority.ManagesProgramAsync(request.TenantId,
                actor, request.ProgramId, ct).ConfigureAwait(false))
            return Result<ControlOccurrenceView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the assigned reviewer or a program manager may review this occurrence."));
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new ControlOperationsLedger(request.TenantId,
                request.ProgramId),
            ledger =>
            {
                var failure = ledger.Review(request.ControlId, request.OccurrenceId,
                    request.ExpectedRevision, request.AttestationId, context.RequestId,
                    request.Outcome, request.Rationale, request.RequestedActions ?? [],
                    actor.MemberId, actor.Display, now, waiver);
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    ledger.ReadOccurrence(request.ControlId, request.OccurrenceId, windows, today)!);
            }, context, ct).ConfigureAwait(false);
    }
}
