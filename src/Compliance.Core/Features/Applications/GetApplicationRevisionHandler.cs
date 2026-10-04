using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class GetApplicationRevisionHandler(IApplicationDirectoryReader directory,
    ApplicationHistoryReadConsistency consistency, RestrictedApplicationVisibility visibility)
    : IRequestHandler<GetApplicationRevision, ApplicationRevisionView>
{
    public async ValueTask<Result<ApplicationRevisionView>> HandleAsync(
        IRequestContext<GetApplicationRevision> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Revision < 1)
            return Result<ApplicationRevisionView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The application revision must be positive."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        if (!await visibility.CanReadApplicationAsync(request.TenantId, userId,
                request.ApplicationId, ct).ConfigureAwait(false))
            return Result<ApplicationRevisionView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The application revision was not found."));
        var freshness = await consistency.EnsureAsync(request.TenantId,
            request.ApplicationId, request.Revision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<ApplicationRevisionView>.Failure(freshness.Error);
        var revision = await directory.GetRevisionAsync(request.TenantId,
            request.ApplicationId, request.Revision, ct).ConfigureAwait(false);
        return revision is not null && revision.TenantId == request.TenantId &&
               revision.ApplicationId == request.ApplicationId &&
               revision.Revision == request.Revision
            ? Result<ApplicationRevisionView>.Success(revision)
            : Result<ApplicationRevisionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The application revision projection is incomplete."));
    }
}
