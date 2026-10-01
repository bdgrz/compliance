using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Projects population identities and status per system instance; replays are idempotent.</summary>
sealed class FitzAccessPopulationDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/access-population-directory-v1/projection",
            AccessReviewDirectorySchema.PopulationProjector),
        IAccessPopulationDirectoryReader, IAccessPopulationDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var directory = AccessReviewDirectorySchema.Populations;
        switch (domainEvent)
        {
            case AccessPopulationOpened opened:
                if (await directory.GetAsync(Transaction, opened.PopulationId, ct).ConfigureAwait(false) is null)
                    await directory.InsertAsync(Transaction, new AccessPopulationSummaryView(
                        opened.TenantId, opened.PopulationId, opened.ApplicationId,
                        opened.SystemInstanceId, opened.SystemInstanceRevision, opened.ObservedAt, 1,
                        AccessPopulation.Draft, null, null, null), ct).ConfigureAwait(false);
                break;
            case AccessPopulationFactsRecorded recorded:
                {
                    var existing = await Required(recorded.PopulationId, ct).ConfigureAwait(false);
                    if (recorded.Revision > existing.Revision)
                        await directory.ReplaceAsync(Transaction, existing,
                            existing with { Revision = recorded.Revision }, ct).ConfigureAwait(false);
                    break;
                }
            case AccessPopulationAccepted accepted:
                {
                    var existing = await Required(accepted.PopulationId, ct).ConfigureAwait(false);
                    if (accepted.Revision > existing.Revision)
                        await directory.ReplaceAsync(Transaction, existing, existing with
                        {
                            Revision = accepted.Revision,
                            Status = AccessPopulation.Accepted,
                            SnapshotId = accepted.SnapshotId,
                            ContentSha256 = accepted.ContentSha256,
                            AcceptedAt = accepted.AcceptedAt,
                        }, ct).ConfigureAwait(false);
                    break;
                }
        }
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(AccessReviewDirectorySchema.PopulationProjector,
            AccessReviewDirectorySchema.PopulationPattern(tenantId)), ct);

    public async ValueTask<Page<AccessPopulationSummaryView>> ListAsync(Uuid tenantId,
        Uuid systemInstanceId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await AccessReviewDirectorySchema.Populations.QueryAsync(tx,
            AccessReviewDirectorySchema.PopulationsByInstance.Query()
                .WithPrefix(systemInstanceId.ToString()).Descending()
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }

    async ValueTask<AccessPopulationSummaryView> Required(Uuid populationId, CancellationToken ct) =>
        await AccessReviewDirectorySchema.Populations.GetAsync(Transaction, populationId, ct)
            .ConfigureAwait(false) ?? throw new InvalidOperationException(
            "A population change cannot project before the population was opened.");
}
