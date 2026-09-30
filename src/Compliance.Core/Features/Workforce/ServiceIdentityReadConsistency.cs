using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class ServiceIdentityReadConsistency(IServiceIdentityDirectoryReader directory,
    IAggregateReader reader, IDomainEventReader events)
{
    public async ValueTask<Result<ServiceIdentityView>> GetAsync(Uuid tenantId,
        Uuid serviceIdentityId, long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<ServiceIdentityView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum service identity revision must be positive."));
        var source = await reader.HydrateAsync(new ServiceIdentity(tenantId, serviceIdentityId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated)
            return Result<ServiceIdentityView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The service identity was not found."));
        var view = await directory.GetAsync(tenantId, serviceIdentityId, ct).ConfigureAwait(false);
        if (view is not null && view.TenantId == tenantId &&
            view.ServiceIdentityId == serviceIdentityId && view.Revision >= source.Revision &&
            (minimumRevision is null || view.Revision >= minimumRevision))
            return Result<ServiceIdentityView>.Success(view);
        return Result<ServiceIdentityView>.Failure(new RequestError(RequestErrorKind.Conflict,
            minimumRevision is { } minimum && source.Revision < minimum
                ? $"The service identity source has not reached revision {minimum}."
                : "The service identity projection has not reached the requested revision.",
            isTransient: true));
    }

    /// <summary>Checks the source area cursor before returning even an empty list.</summary>
    public async ValueTask<Result> EnsureListCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        await using var pending = events.ReadAsync(
            EventStreamPattern.ForPattern(tenantId.ToString(), "service-identities"),
            checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The service identity list projection has not reached the source.",
                isTransient: true))
            : Result.Success;
    }
}
