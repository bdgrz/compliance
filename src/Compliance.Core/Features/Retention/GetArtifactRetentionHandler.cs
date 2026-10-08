using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

sealed class GetArtifactRetentionHandler(ArtifactRetentionRead read) : IRequestHandler<GetArtifactRetention, ArtifactRetentionView>
{
    public ValueTask<Result<ArtifactRetentionView>> HandleAsync(IRequestContext<GetArtifactRetention> context, CancellationToken ct)
    {
        var request = context.Request;
        return read.GetAsync(request.TenantId, request.SourceKind, request.SourceId, request.MinimumRevision, ct);
    }
}
