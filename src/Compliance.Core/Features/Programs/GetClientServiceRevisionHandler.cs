using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class GetClientServiceRevisionHandler(IClientServiceDirectoryReader directory,
    ClientServiceHistoryReadConsistency consistency)
    : IRequestHandler<GetClientServiceRevision, ClientServiceRevisionView>
{
    public async ValueTask<Result<ClientServiceRevisionView>> HandleAsync(
        IRequestContext<GetClientServiceRevision> context, CancellationToken ct)
    {
        var request = context.Request;
        var freshness = await consistency.EnsureAsync(request.TenantId, request.ServiceId,
            request.Revision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<ClientServiceRevisionView>.Failure(freshness.Error);
        var revision = await directory.GetRevisionAsync(request.TenantId, request.ServiceId,
            request.Revision, ct).ConfigureAwait(false);
        return revision is null
            ? Result<ClientServiceRevisionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The service revision was not found."))
            : Result<ClientServiceRevisionView>.Success(revision);
    }
}
