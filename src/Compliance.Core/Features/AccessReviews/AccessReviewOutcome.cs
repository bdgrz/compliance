using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

static class AccessReviewOutcome
{
    /// <summary>Commits the aggregate's raised events only when the command succeeded.</summary>
    public static AggregateOutcome<T> From<T>(Result<T> result) =>
        result.IsSuccess ? AggregateOutcome.Commit(result) : AggregateOutcome.Discard(result);

    public static Result<T> Failure<T>(RequestErrorKind kind, string message,
        bool transient = false) =>
        Result<T>.Failure(new RequestError(kind, message, isTransient: transient));

    /// <summary>True when the projection checkpoint has reached every event in the tenant area.</summary>
    public static async ValueTask<bool> IsCaughtUpAsync(IDomainEventReader events,
        EventStreamPattern pattern, ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(pattern, checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return !await pending.MoveNextAsync().ConfigureAwait(false);
    }
}
