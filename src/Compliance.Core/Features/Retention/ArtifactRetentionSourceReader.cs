using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

static class ArtifactRetentionSourceReader
{
    public static async ValueTask<Result<ArtifactRetentionSourceSnapshot>> LoadAsync(IAggregateReader reader,
        Uuid tenantId, string kind, Uuid id, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || id == Uuid.Empty || !ArtifactRetention.IsSupportedSource(kind))
            return Result<ArtifactRetentionSourceSnapshot>.Failure(new RequestError(RequestErrorKind.Validation,
                "Retention requires a supported tenant source."));
        ArtifactRetentionSourceSnapshot? snapshot = null;
        if (kind == "evidence_artifact")
        {
            var artifact = await reader.HydrateAsync(new EvidenceArtifact(tenantId, id), ct).ConfigureAwait(false);
            if (artifact.IsCreated && artifact.Registration is { } registration && registration.TenantId == tenantId &&
                registration.ArtifactId == id && artifact.Content is { } content && artifact.ContentSha256 is { } sha)
                snapshot = new(new(tenantId, kind, id, sha.ToLowerInvariant()), artifact.CommittedStreamPosition,
                    content.PeriodStart, content.PeriodEnd);
        }
        else
        {
            var batch = await reader.HydrateAsync(new ImportBatch(tenantId, id), ct).ConfigureAwait(false);
            if (batch.IsCreated && batch.SourceObservation is { } staged && staged.TenantId == tenantId &&
                staged.BatchId == id && batch.ContentDigest is { } sha)
                snapshot = new(new(tenantId, kind, id, sha.ToLowerInvariant()), batch.CommittedStreamPosition);
        }
        return snapshot is not null
            ? Result<ArtifactRetentionSourceSnapshot>.Success(snapshot)
            : Result<ArtifactRetentionSourceSnapshot>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The retained source was not found."));
    }

    public static async ValueTask<Result> CheckAsync(IAggregateReader reader,
        ArtifactRetentionSourceSnapshot original, CancellationToken ct)
    {
        var current = await LoadAsync(reader, original.Source.TenantId, original.Source.SourceKind,
            original.Source.SourceId, ct).ConfigureAwait(false);
        return current.IsSuccess && current.Value == original ? Result.Success : Result.Failure(
            new RequestError(RequestErrorKind.Conflict, "The retained source changed during this request.", isTransient: true));
    }
}
