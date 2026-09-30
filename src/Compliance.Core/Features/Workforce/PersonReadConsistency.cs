using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class PersonReadConsistency(IPersonDirectoryReader directory,
    IAggregateReader reader, IDomainEventReader events)
{
    public async ValueTask<Result<PersonView>> GetAsync(Uuid tenantId, Uuid personId,
        long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<PersonView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum person revision must be positive."));
        var source = await reader.HydrateAsync(new Person(tenantId, personId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated)
            return Result<PersonView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The person was not found."));
        var view = await directory.GetAsync(tenantId, personId, ct).ConfigureAwait(false);
        if (view is not null && view.TenantId == tenantId && view.PersonId == personId &&
            view.Revision >= source.Revision &&
            (minimumRevision is null || view.Revision >= minimumRevision))
            return Result<PersonView>.Success(view);
        return Result<PersonView>.Failure(new RequestError(RequestErrorKind.Conflict,
            minimumRevision is { } minimum && source.Revision < minimum
                ? $"The person source has not reached revision {minimum}."
                : "The person projection has not reached the requested revision.",
            isTransient: true));
    }

    /// <summary>Checks the source area cursor before returning even an empty list.</summary>
    public async ValueTask<Result> EnsureListCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var fence = await CaptureListFenceAsync(tenantId, ct).ConfigureAwait(false);
        return fence.IsSuccess ? Result.Success : Result.Failure(fence.Error);
    }

    /// <summary>
    ///     Returns the projection checkpoint once it has reached the source, so a multi-page read can
    ///     later prove with <see cref="EnsureFenceHeldAsync" /> that nothing was projected meanwhile.
    /// </summary>
    public async ValueTask<Result<ProjectionCheckpoint>> CaptureListFenceAsync(Uuid tenantId,
        CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        await using var pending = events.ReadAsync(
            EventStreamPattern.ForPattern(tenantId.ToString(), "people"),
            checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result<ProjectionCheckpoint>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The person list projection has not reached the source.", isTransient: true))
            : Result<ProjectionCheckpoint>.Success(checkpoint);
    }

    /// <summary>Fails transiently when the projection moved past a captured fence.</summary>
    public async ValueTask<Result> EnsureFenceHeldAsync(Uuid tenantId, ProjectionCheckpoint fence,
        CancellationToken ct)
    {
        var current = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        return current.Cursor.Value == fence.Cursor.Value
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The roster changed while it was being read.", isTransient: true));
    }
}
