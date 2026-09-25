using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class GetApplicationRevisionHandler(IApplicationDirectoryReader directory,
    ApplicationHistoryReadConsistency consistency)
    : IRequestHandler<GetApplicationRevision, ApplicationRevisionView>
{
    public async ValueTask<Result<ApplicationRevisionView>> HandleAsync(
        IRequestContext<GetApplicationRevision> context, CancellationToken ct)
    {
        var request = context.Request;
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
