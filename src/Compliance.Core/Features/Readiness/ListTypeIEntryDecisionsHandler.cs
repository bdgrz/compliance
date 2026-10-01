using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Lists the program's Type I entry decisions from one ledger hydration.</summary>
public sealed class ListTypeIEntryDecisionsHandler(IAggregateReader reader)
    : IRequestHandler<ListTypeIEntryDecisions, Page<TypeIEntryDecisionView>>
{
    public async ValueTask<Result<Page<TypeIEntryDecisionView>>> HandleAsync(
        IRequestContext<ListTypeIEntryDecisions> context, CancellationToken ct)
    {
        var request = context.Request;
        var ledger = await reader.HydrateAsync(new ReadinessLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        return ControlActivationSource.Paginate(ledger.TypeIEntryDecisions(), request.Limit,
            request.Cursor, "Type I entry decisions");
    }
}
