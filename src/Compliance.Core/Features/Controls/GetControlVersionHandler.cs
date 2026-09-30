using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class GetControlVersionHandler(ControlActivationSource source)
    : IRequestHandler<GetControlVersion, ControlVersionView>
{
    public async ValueTask<Result<ControlVersionView>> HandleAsync(
        IRequestContext<GetControlVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        var control = await source.LoadAsync(request.TenantId, request.ProgramId,
            request.ControlId, ct).ConfigureAwait(false);
        return control.IsSuccess
            ? ControlActivationSource.Version(control.Value.ReadVersions()
                    .SingleOrDefault(version => version.VersionId == request.VersionId),
                "The approved control version was not found.")
            : Result<ControlVersionView>.Failure(control.Error);
    }
}
