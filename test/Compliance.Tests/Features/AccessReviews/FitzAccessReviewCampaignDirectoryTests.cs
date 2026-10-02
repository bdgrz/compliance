using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessReviews;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class FitzAccessReviewCampaignDirectoryTests
{
    static readonly int[] LaunchOffsets = [0, 2, 1];
    [Fact]
    public async Task ShouldPageNewestCompletedCampaignsGivenPrefixCompatibleBrokerTraversal()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var directory = new FitzAccessReviewCampaignDirectory(new PrefixCompatibleKvClient(new InMemoryKvClient()));
        var now = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Reviewer");
        var launched = LaunchOffsets.Select(offset => new AccessReviewCampaignLaunched(
            tenantId, Uuid.CreateVersion4(), $"Campaign {offset}", "Review", now.AddDays(30),
            Uuid.CreateVersion4(), new string('a', 64), [], [], actor, now.AddMinutes(offset))).ToArray();
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(AccessReviewDirectorySchema.CampaignProjector,
                AccessReviewDirectorySchema.CampaignPattern(tenantId)), ProjectionCheckpoint.Start)))
        {
            foreach (var campaign in launched)
                await directory.ApplyAsync(campaign);
            await directory.ApplyAsync(new AccessReviewCampaignCompleted(tenantId, launched[1].CampaignId, 2,
                new AccessReviewCampaignCompletionView(Uuid.CreateVersion4(), new string('b', 64),
                    "Complete", actor, now.AddDays(1))));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var first = await directory.ListAsync(tenantId, 2, null);
        var second = await directory.ListAsync(tenantId, 2, first.NextCursor);

        // Assert
        Assert.Equal(new[] { launched[1].CampaignId, launched[2].CampaignId },
            first.Items.Select(static row => row.CampaignId));
        Assert.Equal(AccessReviewCampaign.Completed, first.Items[0].Status);
        Assert.NotNull(first.Items[0].FinalSnapshotId);
        Assert.Equal(launched[0].CampaignId, Assert.Single(second.Items).CampaignId);
        Assert.Null(second.NextCursor);
        Assert.Empty((await directory.ListAsync(Uuid.CreateVersion4(), 2, null)).Items);
    }

    sealed class PrefixCompatibleKvClient(IKvClient inner) : IKvClient
    {

        public async Task<IKvTransaction> BeginAsync(string route, KvDurability durability,
            KvMode mode = KvMode.ReadWrite, CancellationToken ct = default) =>
            new PrefixCompatibleTransaction(await inner.BeginAsync(route, durability, mode, ct));

        public Task<KvSubscription> SubscribeAsync(string pattern, CancellationToken ct = default) =>
            inner.SubscribeAsync(pattern, ct);

        sealed class PrefixCompatibleTransaction(IKvTransaction inner) : IKvTransaction
        {
            public string Route => inner.Route;
            public Task<KvGetResult> GetAsync(ReadOnlyMemory<byte> key, CancellationToken ct = default) =>
                inner.GetAsync(key, ct);
            public Task PutAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value,
                CancellationToken ct = default) => inner.PutAsync(key, value, ct);
            public Task InsertAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value,
                CancellationToken ct = default) => inner.InsertAsync(key, value, ct);
            public Task DeleteAsync(ReadOnlyMemory<byte> key, CancellationToken ct = default) =>
                inner.DeleteAsync(key, ct);
            public Task DeleteRangeAsync(ReadOnlyMemory<byte> startKey, ReadOnlyMemory<byte> endKey,
                CancellationToken ct = default) => inner.DeleteRangeAsync(startKey, endKey, ct);
            public async Task<KvScanResult> ScanAsync(KvScanQuery query, CancellationToken ct = default)
            {
                // Reproduce the deployed broker's empty reverse prefix-range traversal (#498).
                if (query.Reverse && query.StartKey is not null && query.EndKey is not null)
                    return new KvScanResult([], false);
                var result = await inner.ScanAsync(query, ct);

                return result;
            }
            public Task CommitAsync(CancellationToken ct = default) => inner.CommitAsync(ct);
            public Task RollbackAsync(CancellationToken ct = default) => inner.RollbackAsync(ct);
            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
