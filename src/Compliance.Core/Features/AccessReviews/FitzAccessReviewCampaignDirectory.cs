using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Projects campaign identities and status; replays are idempotent.</summary>
sealed class FitzAccessReviewCampaignDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/access-review-campaign-directory-v1/projection",
            AccessReviewDirectorySchema.CampaignProjector),
        IAccessReviewCampaignDirectoryReader, IAccessReviewCampaignDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var directory = AccessReviewDirectorySchema.Campaigns;
        switch (domainEvent)
        {
            case AccessReviewCampaignLaunched launched:
                if (await directory.GetAsync(Transaction, launched.CampaignId, ct).ConfigureAwait(false) is null)
                    await directory.InsertAsync(Transaction, new AccessReviewCampaignSummaryView(
                        launched.TenantId, launched.CampaignId, launched.Name, launched.Deadline,
                        AccessReviewCampaign.Active, launched.Items.Count, launched.SnapshotId,
                        launched.LaunchedAt, null, null), ct).ConfigureAwait(false);
                break;
            case AccessReviewCampaignCompleted completed:
                {
                    var existing = await directory.GetAsync(Transaction, completed.CampaignId, ct)
                        .ConfigureAwait(false) ?? throw new InvalidOperationException(
                        "A completion cannot project before the campaign was launched.");
                    if (existing.Status != AccessReviewCampaign.Completed)
                        await directory.ReplaceAsync(Transaction, existing, existing with
                        {
                            Status = AccessReviewCampaign.Completed,
                            FinalSnapshotId = completed.Completion.SnapshotId,
                            CompletedAt = completed.Completion.CompletedAt,
                        }, ct).ConfigureAwait(false);
                    break;
                }
        }
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(AccessReviewDirectorySchema.CampaignProjector,
            AccessReviewDirectorySchema.CampaignPattern(tenantId)), ct);

    public async ValueTask<Page<AccessReviewCampaignSummaryView>> ListAsync(Uuid tenantId,
        int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await AccessReviewDirectorySchema.Campaigns.QueryAsync(tx,
            AccessReviewDirectorySchema.CampaignsByLaunch.Query().Descending()
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }
}
