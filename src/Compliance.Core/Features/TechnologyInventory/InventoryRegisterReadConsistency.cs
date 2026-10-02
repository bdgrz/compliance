using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Serves location and process reads only once the projection has caught up.</summary>
public sealed class InventoryRegisterReadConsistency(IInventoryRegisterReader directory,
    IAggregateReader reader, IDomainEventReader events)
{
    public async ValueTask<Result<LocationView>> GetLocationAsync(Uuid tenantId, Uuid id,
        long? minimumRevision, CancellationToken ct)
    {
        var source = await reader.HydrateAsync(new LocationRegister(tenantId), ct)
            .ConfigureAwait(false);
        return await ServeAsync("location", source.RevisionOf(id), minimumRevision,
            async () => await directory.GetLocationAsync(tenantId, id, ct)
                .ConfigureAwait(false) is { } view && view.TenantId == tenantId &&
                view.LocationId == id
                ? (view, view.Revision)
                : default).ConfigureAwait(false);
    }

    public async ValueTask<Result<OperationalProcessView>> GetOperationalProcessAsync(
        Uuid tenantId, Uuid id, long? minimumRevision, CancellationToken ct)
    {
        var source = await reader.HydrateAsync(new OperationalProcessRegister(tenantId), ct)
            .ConfigureAwait(false);
        return await ServeAsync("operational process", source.RevisionOf(id), minimumRevision,
            async () => await directory.GetOperationalProcessAsync(tenantId, id, ct)
                .ConfigureAwait(false) is { } view && view.TenantId == tenantId &&
                view.OperationalProcessId == id
                ? (view, view.Revision)
                : default).ConfigureAwait(false);
    }

    /// <summary>Checks the source area cursor before returning even an empty list.</summary>
    public async ValueTask<Result> EnsureListCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        await using var pending = events.ReadAsync(InventoryRegisters.TenantPattern(tenantId),
            checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The inventory register projection has not reached the source.",
                isTransient: true))
            : Result.Success;
    }

    static async ValueTask<Result<TView>> ServeAsync<TView>(string noun, long sourceRevision,
        long? minimumRevision, Func<ValueTask<(TView? View, long Revision)>> load)
        where TView : class
    {
        if (minimumRevision is < 1)
            return Result<TView>.Failure(new RequestError(RequestErrorKind.Validation,
                $"The minimum {noun} revision must be positive."));
        if (sourceRevision < 1)
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
