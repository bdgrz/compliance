using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class ListControlDecisionsHandler(ControlActivationSource source)
    : IRequestHandler<ListControlDecisions, Page<ControlDecisionView>>
{
    public async ValueTask<Result<Page<ControlDecisionView>>> HandleAsync(
        IRequestContext<ListControlDecisions> context, CancellationToken ct)
    {
        var request = context.Request;
        var control = await source.LoadAsync(request.TenantId, request.ProgramId,
            request.ControlId, ct).ConfigureAwait(false);
        return control.IsSuccess
            ? ControlActivationSource.Paginate(control.Value.ReadDecisions(), request.Limit,
                request.Cursor, "control decision")
            : Result<Page<ControlDecisionView>>.Failure(control.Error);
    }
}
