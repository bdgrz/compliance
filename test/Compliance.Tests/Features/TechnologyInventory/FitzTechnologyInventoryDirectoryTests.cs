using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory;

public sealed class FitzTechnologyInventoryDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Fact]
    public async Task ShouldProjectCurrentAndHistoryAndIsolateTenantsGivenAssetRevisions()
    {
        // Arrange
        var directory = new FitzTechnologyInventoryDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var assetId = Uuid.CreateVersion4();
        var first = new InformationAssetContent("Customer PII", "confidential", "M0-D16",
            Uuid.CreateVersion4(), null, "active");
        var second = first with { Classification = "restricted" };

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new InformationAssetRevisionRecorded(tenantId, assetId, 1,
                first, Author, Now));
            await directory.ApplyAsync(new InformationAssetRevisionRecorded(tenantId, assetId, 2,
                second, Author, Now.AddMinutes(1)));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(new InformationAssetRevisionRecorded(tenantId,
                    assetId, 4, second, Author, Now)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var current = await directory.GetAssetAsync(tenantId, assetId);
        var history = await directory.ListAssetRevisionsAsync(tenantId, assetId, 50, null);
        var listed = await directory.ListAssetsAsync(tenantId, 50, null);
        var alien = await directory.ListAssetsAsync(Uuid.CreateVersion4(), 50, null);

        // Assert
        Assert.NotNull(current);
        Assert.Equal(2, current.Revision);
        Assert.Equal("restricted", current.Content.Classification);
        Assert.Equal("manual", current.SourceKind);
        Assert.Equal([1L, 2L], history.Items.Select(static item => item.Revision));
        Assert.Equal("confidential", history.Items[0].Content.Classification);
        Assert.Single(listed.Items);
        Assert.Empty(alien.Items);
    }

    static CheckpointIdentity Identity(Uuid tenantId) =>
        new(FitzTechnologyInventoryDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "technology-inventory"));
}
