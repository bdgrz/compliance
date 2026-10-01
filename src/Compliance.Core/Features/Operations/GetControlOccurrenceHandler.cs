using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Reads one occurrence with every attestation version and review decision.</summary>
public sealed class GetControlOccurrenceHandler(IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<GetControlOccurrence, ControlOccurrenceView>
{
    public async ValueTask<Result<ControlOccurrenceView>> HandleAsync(
        IRequestContext<GetControlOccurrence> context, CancellationToken ct)
    {
        var request = context.Request;
        var control = await ControlOperationsSource.LoadControlAsync(reader, request.TenantId,
            request.ProgramId, request.ControlId, ct).ConfigureAwait(false);
        if (control is null)
            return Result<ControlOccurrenceView>.Failure(ControlOperationsSource.ControlNotFound());
        var ledger = await reader.HydrateAsync(new ControlOperationsLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        return ledger.ReadOccurrence(request.ControlId, request.OccurrenceId,
                OperatingAuthority.VersionWindows(control), ControlOperationsSource.Today(clock)) is
        { } view
            ? Result<ControlOccurrenceView>.Success(view)
            : Result<ControlOccurrenceView>.Failure(ControlOperationsSource.OccurrenceNotFound());
    }
}
