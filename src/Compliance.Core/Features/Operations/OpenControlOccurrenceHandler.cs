using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Opens an event-driven or ad hoc occurrence for its holder or a program manager.</summary>
public sealed class OpenControlOccurrenceHandler(IAggregateExecutor executor,
    IAggregateReader reader, OperatingAuthority authority, TimeProvider clock)
    : IRequestHandler<OpenControlOccurrence, ControlOccurrenceView>
{
    public async ValueTask<Result<ControlOccurrenceView>> HandleAsync(
        IRequestContext<OpenControlOccurrence> context, CancellationToken ct)
    {
        var request = context.Request;
        var control = await ControlOperationsSource.LoadControlAsync(reader, request.TenantId,
            request.ProgramId, request.ControlId, ct).ConfigureAwait(false);
        if (control is null)
            return Result<ControlOccurrenceView>.Failure(ControlOperationsSource.ControlNotFound());
        var current = await reader.HydrateAsync(new ControlOperationsLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        if (current.PlanOn(request.ControlId, request.OccurredOn) is { } plan &&
            !await authority.HoldsAsync(request.TenantId, plan.Owner, actor.MemberId, ct)
                .ConfigureAwait(false) &&
            !await authority.HoldsAsync(request.TenantId, plan.BackupOwner, actor.MemberId, ct)
                .ConfigureAwait(false) &&
            !await authority.ManagesProgramAsync(request.TenantId, actor, request.ProgramId, ct)
                .ConfigureAwait(false))
            return Result<ControlOccurrenceView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only an operating holder or a program manager may open an occurrence."));
        var windows = OperatingAuthority.VersionWindows(control);
        var now = clock.GetUtcNow();
        return await executor.ExecuteAsync(new ControlOperationsLedger(request.TenantId,
                request.ProgramId),
            ledger =>
            {
                var failure = ledger.OpenOccurrence(request.ControlId, context.RequestId,
                    request.Trigger, request.OccurredOn, windows, actor.MemberId, actor.Display,
                    now);
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    ledger.ReadOccurrence(request.ControlId, context.RequestId, windows,
                        DateOnly.FromDateTime(now.UtcDateTime))!);
            }, context, ct).ConfigureAwait(false);
    }
}
