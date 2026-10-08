using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

sealed class GetEvidenceArtifactMetadataHandler(EvidenceArtifactReadAccess access, EvidenceArtifactMetadataRead read)
    : IRequestHandler<GetEvidenceArtifactMetadata, EvidenceArtifactMetadataView>
{
    public async ValueTask<Result<EvidenceArtifactMetadataView>> HandleAsync(IRequestContext<GetEvidenceArtifactMetadata> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var allowed = await access.RequireAsync(context, request.TenantId, request.ArtifactId, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<EvidenceArtifactMetadataView>.Failure(allowed.Error);
        var result = await read.GetAsync(request.TenantId, request.ArtifactId, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return result;
        allowed = await access.RequireAsync(context, request.TenantId, request.ArtifactId, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<EvidenceArtifactMetadataView>.Failure(allowed.Error);
        var unchanged = await read.CheckAsync(result.Value, ct).ConfigureAwait(false);
        return unchanged.IsSuccess ? result : Result<EvidenceArtifactMetadataView>.Failure(unchanged.Error);
    }
}
