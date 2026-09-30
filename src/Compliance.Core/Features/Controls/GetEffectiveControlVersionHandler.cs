using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class GetEffectiveControlVersionHandler(ControlActivationSource source)
    : IRequestHandler<GetEffectiveControlVersion, ControlVersionView>
{
    public async ValueTask<Result<ControlVersionView>> HandleAsync(
        IRequestContext<GetEffectiveControlVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.EffectiveOn == default)
            return Result<ControlVersionView>.Failure(new RequestError(
                RequestErrorKind.Validation, "An effective date is required."));
        var control = await source.LoadAsync(request.TenantId, request.ProgramId,
            request.ControlId, ct).ConfigureAwait(false);
        return control.IsSuccess
            ? ControlActivationSource.Version(control.Value.EffectiveVersion(request.EffectiveOn),
                "No approved control version is effective on that date.")
            : Result<ControlVersionView>.Failure(control.Error);
    }
}
