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
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        await using var pending = events.ReadAsync(
            EventStreamPattern.ForPattern(tenantId.ToString(), "people"),
            checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The person list projection has not reached the source.", isTransient: true))
            : Result.Success;
    }
}
