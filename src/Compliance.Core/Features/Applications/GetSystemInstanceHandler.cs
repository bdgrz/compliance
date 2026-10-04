using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class GetSystemInstanceHandler(IApplicationDirectoryReader directory,
    SystemInstanceReadConsistency consistency, RestrictedApplicationVisibility visibility)
    : IRequestHandler<GetSystemInstance, SystemInstanceView>
{
    public async ValueTask<Result<SystemInstanceView>> HandleAsync(
        IRequestContext<GetSystemInstance> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.MinimumApplicationRevision is < 1)
            return Result<SystemInstanceView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum application revision must be positive."));
        if (request.MinimumInstanceRevision is < 1)
            return Result<SystemInstanceView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum system instance revision must be positive."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, userId,
                request.ApplicationId, request.SystemInstanceId, ct).ConfigureAwait(false))
            return Result<SystemInstanceView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The system instance was not found."));
        var freshness = await consistency.EnsureAsync(request.TenantId, request.ApplicationId,
            request.MinimumApplicationRevision, request.SystemInstanceId,
            request.MinimumInstanceRevision, ct).ConfigureAwait(false);
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
