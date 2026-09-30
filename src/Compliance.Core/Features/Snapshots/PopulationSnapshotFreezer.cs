using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

/// <summary>
///     Freezes caller-assembled population rows as an immutable snapshot keyed by the request ID,
///     so a retried request replays instead of freezing twice. Consumers own source capture and
///     authorization; this primitive owns identity, lineage, and the single commit.
/// </summary>
public sealed class PopulationSnapshotFreezer(IAggregateReader reader,
    IAggregateExecutor executor, TimeProvider clock)
{
    public async ValueTask<Result<SnapshotRegistration>> FreezeAsync<TRequest>(
        IRequestContext<TRequest> context, Uuid tenantId, string kind,
        IReadOnlyList<PopulationRow> rows, Uuid? amendsSnapshotId, string? amendmentReason,
        ActorReference frozenBy, CancellationToken ct)
        where TRequest : IRequestBase
    {
        if ((amendsSnapshotId is null) != (amendmentReason is null) ||
            (amendmentReason is not null &&
             (string.IsNullOrWhiteSpace(amendmentReason) || amendmentReason.Length > 4000)))
            return Failure(RequestErrorKind.Validation,
                "An amendment requires its predecessor and a reason of at most 4000 characters.");
        if (rows.Count > PopulationSnapshot.MaximumInlineRows)
            return Failure(RequestErrorKind.Validation,
                $"A population snapshot currently supports at most {PopulationSnapshot.MaximumInlineRows} rows.");
        var digest = PopulationContentIdentity.Compute(kind, rows);
        if (!digest.IsSuccess)
            return Result<SnapshotRegistration>.Failure(digest.Error);

        var snapshotId = context.RequestId;
        var rootSnapshotId = snapshotId;
        if (amendsSnapshotId is { } priorId)
        {
            if (priorId == snapshotId)
                return Failure(RequestErrorKind.Validation,
                    "An amendment cannot name itself as its predecessor.");
            var prior = await reader.HydrateAsync(new PopulationSnapshot(tenantId, priorId), ct)
                .ConfigureAwait(false);
            if (!prior.IsFrozen || prior.Kind != kind)
                return Failure(RequestErrorKind.NotFound, "The prior snapshot was not found.");
            if (!await HasBoundedLineageAsync(tenantId, prior, ct).ConfigureAwait(false))
                return Failure(RequestErrorKind.Conflict,
                    "The prior snapshot lineage is invalid or has reached the supported amendment limit.");
            rootSnapshotId = prior.RootSnapshotId;
        }

        return await executor.ExecuteAsync(new PopulationSnapshot(tenantId, snapshotId),
            snapshot => AggregateOutcome.CommitOnSuccess(snapshot.Freeze(rootSnapshotId,
                amendsSnapshotId, kind, rows, digest.Value.Sha256, amendmentReason, frozenBy,
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }

    async ValueTask<bool> HasBoundedLineageAsync(Uuid tenantId, PopulationSnapshot leaf,
        CancellationToken ct)
    {
        var seen = new HashSet<Uuid> { leaf.Id };
        var current = leaf;
        for (var links = 0; current.AmendsSnapshotId is { } predecessorId; links++)
        {
            if (links + 1 >= SnapshotAmendmentLineage.MaximumAmendmentLinks ||
                !seen.Add(predecessorId))
                return false;
            var predecessor = await reader.HydrateAsync(
                new PopulationSnapshot(tenantId, predecessorId), ct).ConfigureAwait(false);
            if (!predecessor.IsFrozen || predecessor.Kind != leaf.Kind ||
                predecessor.RootSnapshotId != leaf.RootSnapshotId)
                return false;
            current = predecessor;
        }
        return current.Id == leaf.RootSnapshotId;
    }

    static Result<SnapshotRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<SnapshotRegistration>.Failure(new RequestError(kind, message));
}
