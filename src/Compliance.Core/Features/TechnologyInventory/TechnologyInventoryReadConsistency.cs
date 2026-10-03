using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Serves inventory reads only once the projection has caught up with the source.</summary>
public sealed class TechnologyInventoryReadConsistency(ITechnologyInventoryReader directory,
    IAggregateReader reader, IDomainEventReader events)
{
    public async ValueTask<Result<TechnologyComponentView>> GetComponentAsync(Uuid tenantId,
        Uuid id, long? minimumRevision, CancellationToken ct)
    {
        var source = await reader.HydrateAsync(new TechnologyComponent(tenantId, id), ct)
            .ConfigureAwait(false);
        return await ServeAsync("technology component", source.IsCreated, source.Revision,
            minimumRevision, async () => await directory.GetComponentAsync(tenantId, id, ct)
                .ConfigureAwait(false) is { } view && view.TenantId == tenantId &&
                view.ComponentId == id
                ? (view, view.Revision)
                : default).ConfigureAwait(false);
    }

    public async ValueTask<Result<InformationAssetView>> GetAssetAsync(Uuid tenantId, Uuid id,
        long? minimumRevision, CancellationToken ct)
    {
        var source = await reader.HydrateAsync(new InformationAsset(tenantId, id), ct)
            .ConfigureAwait(false);
        return await ServeAsync("information asset", source.IsCreated, source.Revision,
            minimumRevision, async () => await directory.GetAssetAsync(tenantId, id, ct)
                .ConfigureAwait(false) is { } view && view.TenantId == tenantId &&
                view.InformationAssetId == id
                ? (view, view.Revision)
                : default).ConfigureAwait(false);
    }

    public async ValueTask<Result<DataFlowView>> GetFlowAsync(Uuid tenantId, Uuid id,
        long? minimumRevision, CancellationToken ct)
    {
        var source = await reader.HydrateAsync(new DataFlow(tenantId, id), ct)
            .ConfigureAwait(false);
        return await ServeAsync("data flow", source.IsCreated, source.Revision,
            minimumRevision, async () => await directory.GetFlowAsync(tenantId, id, ct)
                .ConfigureAwait(false) is { } view && view.TenantId == tenantId &&
                view.DataFlowId == id
                ? (view, view.Revision)
                : default).ConfigureAwait(false);
    }

    /// <summary>Checks the source area cursor before returning even an empty list.</summary>
    public async ValueTask<Result> EnsureListCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var capture = await CaptureAsync(tenantId, ct).ConfigureAwait(false);
        return capture.IsSuccess ? Result.Success : Result.Failure(capture.Error);
    }

    public async ValueTask<Result<ProjectionCheckpoint>> CaptureAsync(Uuid tenantId,
        CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        return await HasPendingSourceAsync(tenantId, checkpoint, ct).ConfigureAwait(false)
            ? Result<ProjectionCheckpoint>.Failure(BehindSourceError())
            : Result<ProjectionCheckpoint>.Success(checkpoint);
    }

    public async ValueTask<Result> ConfirmUnchangedAndCaughtUpAsync(Uuid tenantId,
        ProjectionCheckpoint fence, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        if (checkpoint != fence ||
            await HasPendingSourceAsync(tenantId, checkpoint, ct).ConfigureAwait(false))
            return Result.Failure(BehindSourceError());
        return Result.Success;
    }

    async ValueTask<bool> HasPendingSourceAsync(Uuid tenantId,
        ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(
            TechnologyInventoryStreams.TenantPattern(tenantId), checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static RequestError BehindSourceError() => new(RequestErrorKind.Conflict,
        "The technology inventory projection changed or has not reached the source. Retry the query.",
        isTransient: true);

    static async ValueTask<Result<TView>> ServeAsync<TView>(string noun, bool created,
        long sourceRevision, long? minimumRevision,
        Func<ValueTask<(TView? View, long Revision)>> load) where TView : class
    {
        if (minimumRevision is < 1)
            return Result<TView>.Failure(new RequestError(RequestErrorKind.Validation,
                $"The minimum {noun} revision must be positive."));
        if (!created)
            return Result<TView>.Failure(new RequestError(RequestErrorKind.NotFound,
                $"The {noun} was not found."));
        var (view, revision) = await load().ConfigureAwait(false);
        if (view is not null && revision >= sourceRevision &&
            (minimumRevision is null || revision >= minimumRevision))
            return Result<TView>.Success(view);
        return Result<TView>.Failure(new RequestError(RequestErrorKind.Conflict,
            minimumRevision is { } minimum && sourceRevision < minimum
                ? $"The {noun} source has not reached revision {minimum}."
                : $"The {noun} projection has not reached the requested revision.",
            isTransient: true));
    }
}
