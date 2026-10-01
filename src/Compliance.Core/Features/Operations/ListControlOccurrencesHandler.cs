using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Lists a control's occurrence population: recorded occurrences plus expected periods that
///     start within the next 90 days, ordered by due date.
/// </summary>
public sealed class ListControlOccurrencesHandler(IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<ListControlOccurrences, Page<ControlOccurrenceView>>
{
    const int HorizonDays = 90;

    public async ValueTask<Result<Page<ControlOccurrenceView>>> HandleAsync(
        IRequestContext<ListControlOccurrences> context, CancellationToken ct)
    {
        var request = context.Request;
        var control = await ControlOperationsSource.LoadControlAsync(reader, request.TenantId,
            request.ProgramId, request.ControlId, ct).ConfigureAwait(false);
        if (control is null)
            return Result<Page<ControlOccurrenceView>>.Failure(
                ControlOperationsSource.ControlNotFound());
        var ledger = await reader.HydrateAsync(new ControlOperationsLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        var today = ControlOperationsSource.Today(clock);
        var items = ledger.ReadOccurrences(request.ControlId,
                OperatingAuthority.VersionWindows(control), today, today.AddDays(HorizonDays))
            .Where(view => request.State is null || view.State == request.State)
            .ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor,
            "control occurrences");
    }
}
