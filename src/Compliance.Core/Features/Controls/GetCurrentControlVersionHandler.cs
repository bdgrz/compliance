using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class GetCurrentControlVersionHandler(ControlActivationSource source)
    : IRequestHandler<GetCurrentControlVersion, ControlVersionView>
{
    public async ValueTask<Result<ControlVersionView>> HandleAsync(
        IRequestContext<GetCurrentControlVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        var control = await source.LoadAsync(request.TenantId, request.ProgramId,
            request.ControlId, ct).ConfigureAwait(false);
        return control.IsSuccess
            ? ControlActivationSource.Version(control.Value.ApprovedVersion,
                "The control has no approved version.")
            : Result<ControlVersionView>.Failure(control.Error);
    }
}
