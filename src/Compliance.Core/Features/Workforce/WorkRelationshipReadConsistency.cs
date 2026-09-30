using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class WorkRelationshipReadConsistency(IWorkRelationshipDirectoryReader directory,
    IAggregateReader reader, IDomainEventReader events)
{
    public async ValueTask<Result<WorkRelationshipView>> GetAsync(Uuid tenantId, Uuid relationshipId,
        long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<WorkRelationshipView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum work relationship revision must be positive."));
        var source = await reader.HydrateAsync(new WorkRelationship(tenantId, relationshipId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated)
            return Result<WorkRelationshipView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The work relationship was not found."));
        var view = await directory.GetAsync(tenantId, relationshipId, ct).ConfigureAwait(false);
        if (view is not null && view.TenantId == tenantId && view.RelationshipId == relationshipId &&
            view.Revision >= source.Revision &&
            (minimumRevision is null || view.Revision >= minimumRevision))
            return Result<WorkRelationshipView>.Success(view);
        return Result<WorkRelationshipView>.Failure(new RequestError(RequestErrorKind.Conflict,
            minimumRevision is { } minimum && source.Revision < minimum
                ? $"The work relationship source has not reached revision {minimum}."
                : "The work relationship projection has not reached the requested revision.",
            isTransient: true));
    }

    /// <summary>Checks the source area cursor before returning even an empty list.</summary>
    public async ValueTask<Result> EnsureListCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        await using var pending = events.ReadAsync(
            EventStreamPattern.ForPattern(tenantId.ToString(), "work-relationships"),
            checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The work relationship list projection has not reached the source.", isTransient: true))
            : Result.Success;
    }
}
