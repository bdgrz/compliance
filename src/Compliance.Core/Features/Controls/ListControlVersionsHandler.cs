using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class ListControlVersionsHandler(ControlActivationSource source)
    : IRequestHandler<ListControlVersions, Page<ControlVersionView>>
{
    public async ValueTask<Result<Page<ControlVersionView>>> HandleAsync(
        IRequestContext<ListControlVersions> context, CancellationToken ct)
    {
        var request = context.Request;
        var control = await source.LoadAsync(request.TenantId, request.ProgramId,
            request.ControlId, ct).ConfigureAwait(false);
        if (!control.IsSuccess)
            return Result<Page<ControlVersionView>>.Failure(control.Error);
        return ControlActivationSource.Paginate(control.Value.ReadVersions(), request.Limit, request.Cursor,
            "control version");
    }
}
