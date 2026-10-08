using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

sealed class GetApplicationImportRetentionHandler(ArtifactRetentionRead read) : IRequestHandler<GetApplicationImportRetention, ArtifactRetentionView>
{
    public ValueTask<Result<ArtifactRetentionView>> HandleAsync(IRequestContext<GetApplicationImportRetention> context, CancellationToken ct)
    {
        var request = context.Request;
        return read.GetAsync(request.TenantId, "application_import", request.BatchId, request.MinimumRevision, ct);
    }
}
