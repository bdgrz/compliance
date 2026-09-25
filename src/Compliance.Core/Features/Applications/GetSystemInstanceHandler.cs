using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class GetSystemInstanceHandler(IApplicationDirectoryReader directory,
    SystemInstanceReadConsistency consistency)
    : IRequestHandler<GetSystemInstance, SystemInstanceView>
{
    public async ValueTask<Result<SystemInstanceView>> HandleAsync(
        IRequestContext<GetSystemInstance> context, CancellationToken ct)
    {
        var request = context.Request;
        var freshness = await consistency.EnsureAsync(request.TenantId, request.ApplicationId,
            request.MinimumApplicationRevision, request.SystemInstanceId, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<SystemInstanceView>.Failure(freshness.Error);
        var view = await directory.GetInstanceAsync(request.TenantId, request.SystemInstanceId, ct)
            .ConfigureAwait(false);
        if (view is null)
            return Result<SystemInstanceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The system instance projection is incomplete.", isTransient: true));
        return view.TenantId != request.TenantId ||
               view.ApplicationId != request.ApplicationId ||
               view.SystemInstanceId != request.SystemInstanceId
            ? Result<SystemInstanceView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The system instance was not found."))
            : Result<SystemInstanceView>.Success(view);
    }
}
