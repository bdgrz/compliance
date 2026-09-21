using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class FitzApplicationImportDirectoryTests
{
    [Fact]
    public async Task ShouldPageAllRowsWithoutTenantDisclosureGivenTwoHundredRowBatch()
    {
        // Arrange
        var directory = new FitzApplicationImportDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var batchId = Uuid.CreateVersion4();
        var identity = Checkpoint(tenantId);
        await using (var projection = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(Staged(tenantId, batchId, 200));
            await projection.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var first = await directory.ListRowsAsync(tenantId, batchId, 137, null);
        var second = await directory.ListRowsAsync(tenantId, batchId, 137, first.NextCursor);
        var alienBatch = await directory.GetAsync(otherTenantId, batchId);
        var alienRows = await directory.ListRowsAsync(otherTenantId, batchId, 200, null);

        // Assert
        Assert.Equal(137, first.Items.Count);
        Assert.Equal(63, second.Items.Count);
        Assert.Equal(Enumerable.Range(1, 200), first.Items.Concat(second.Items)
            .Select(row => row.RowNumber));
        Assert.Null(alienBatch);
        Assert.Empty(alienRows.Items);
        Assert.Equal(200, (await directory.GetAsync(tenantId, batchId))?.RowCount);
    }

    [Fact]
    public async Task ShouldLeaveNoBatchOrRowsGivenAbortedProjectionBatch()
    {
        // Arrange
        var directory = new FitzApplicationImportDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var batchId = Uuid.CreateVersion4();

        // Act
        await using (var projection = await directory.BeginAsync(
                         new ProjectionBatchContext(Checkpoint(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(Staged(tenantId, batchId, 3));
        }
        var missing = await directory.GetAsync(tenantId, batchId);
        var missingRows = await directory.ListRowsAsync(tenantId, batchId, 200, null);
        await using (var retry = await directory.BeginAsync(
                         new ProjectionBatchContext(Checkpoint(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(Staged(tenantId, batchId, 3));
            await retry.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Null(missing);
        Assert.Empty(missingRows.Items);
        Assert.Equal(3, (await directory.GetAsync(tenantId, batchId))?.RowCount);
        Assert.Equal(3, (await directory.ListRowsAsync(tenantId, batchId, 200, null)).Items.Count);
    }

    static CheckpointIdentity Checkpoint(Uuid tenantId) => new(
        "ApplicationImportDirectoryV1", EventStreamPattern.ForPattern(tenantId.ToString()));

    static ApplicationImportStaged Staged(Uuid tenantId, Uuid batchId, int rowCount) =>
        new(tenantId, batchId, Uuid.CreateVersion4(), "source", "primary", "partial",
            new string('A', 64), Enumerable.Range(1, rowCount)
                .Select(number => new ApplicationImportStagedRow(Uuid.CreateVersion4(), number,
                    $"record-{number}", $"Application {number}", "Purpose", null, []))
                .ToArray(), Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow);
}
