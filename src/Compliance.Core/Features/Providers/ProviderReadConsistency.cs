using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed class ProviderReadConsistency(IProviderReader directory, IAggregateReader reader, IDomainEventReader events)
{
    public async ValueTask<Result<ProviderView>> GetAsync(Uuid tenantId, Uuid providerId,
        long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Failure(RequestErrorKind.Validation, "The minimum provider revision must be positive.");
        var source = await reader.HydrateAsync(new ProviderRegister(tenantId), ct).ConfigureAwait(false);
        var current = source.Get(providerId);
        if (current is null || current.TenantId != tenantId)
            return Failure(RequestErrorKind.NotFound, "The provider was not found.");
        if (minimumRevision is { } minimum && current.Revision < minimum)
            return Failure(RequestErrorKind.Conflict, "The provider source has not reached the requested revision.");
        var view = await directory.GetAsync(tenantId, providerId, ct).ConfigureAwait(false);
        return Matches(view, tenantId, providerId) && view!.Revision == current.Revision
            ? Result<ProviderView>.Success(view)
            : Failure(RequestErrorKind.Conflict, "The provider projection has not reached the source revision.");
    }

    public async ValueTask<Result<ProviderView>> GetRevisionAsync(Uuid tenantId, Uuid providerId,
        long revision, CancellationToken ct)
    {
        if (revision < 1)
            return Failure(RequestErrorKind.Validation, "The provider revision must be positive.");
        var source = await reader.HydrateAsync(new ProviderRegister(tenantId), ct).ConfigureAwait(false);
        var current = source.Get(providerId);
        if (current is null || current.TenantId != tenantId)
            return Failure(RequestErrorKind.NotFound, "The provider was not found.");
        if (revision > current.Revision)
            return Failure(RequestErrorKind.NotFound, "The provider revision was not found.");
        var view = await directory.GetRevisionAsync(tenantId, providerId, revision, ct).ConfigureAwait(false);
        return Matches(view, tenantId, providerId) && view!.Revision == revision
            ? Result<ProviderView>.Success(view)
            : Failure(RequestErrorKind.Conflict, "The provider revision projection is incomplete.");
    }

    public async ValueTask<Result> EnsureListCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        await using var pending = events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
            ProviderRegister.Area), checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The provider list projection has not reached the source.", isTransient: true))
            : Result.Success;
    }

    static bool Matches(ProviderView? view, Uuid tenantId, Uuid providerId) =>
        view is not null && view.TenantId == tenantId && view.ProviderId == providerId;

    static Result<ProviderView> Failure(RequestErrorKind kind, string message) =>
        Result<ProviderView>.Failure(new RequestError(kind, message, isTransient: kind == RequestErrorKind.Conflict));
}
