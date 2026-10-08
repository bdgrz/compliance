using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

sealed class ArtifactRetentionRead(IAggregateReader reader, TimeProvider clock)
{
    public async ValueTask<Result<ArtifactRetentionView>> GetAsync(Uuid tenant, string kind, Uuid id,
        long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<ArtifactRetentionView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum retention revision must be positive."));
        var source = await ArtifactRetentionSourceReader.LoadAsync(reader, tenant, kind, id, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<ArtifactRetentionView>.Failure(source.Error);
        var retention = await reader.HydrateAsync(new ArtifactRetention(tenant, kind, id), ct).ConfigureAwait(false);
        var valid = retention.ValidateSource(source.Value);
        if (!valid.IsSuccess)
            return Result<ArtifactRetentionView>.Failure(valid.Error);
        if (minimumRevision > retention.Revision)
            return Result<ArtifactRetentionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The requested retention revision is not yet durable.", isTransient: true));
        var fence = await ArtifactRetentionSourceReader.CheckAsync(reader, source.Value, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<ArtifactRetentionView>.Failure(fence.Error);
        var current = await reader.HydrateAsync(new ArtifactRetention(tenant, kind, id), ct).ConfigureAwait(false);
        if (current.CommittedStreamPosition != retention.CommittedStreamPosition)
            return Result<ArtifactRetentionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Retention changed during this request.", isTransient: true));
        return Result<ArtifactRetentionView>.Success(retention.Assess(source.Value.Source,
            DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)));
    }
}
