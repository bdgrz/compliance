using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory;

public sealed class FitzInventoryRegisterDirectoryTests
{
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder");
    static readonly Uuid Owner = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldExhaustScopedPagesAndHistoryGivenTwoTenantsOfLocationsAndProcesses()
    {
        // Arrange
        var directory = new FitzInventoryRegisterDirectory(new InMemoryKvClient());
        Uuid[] tenants = [Uuid.CreateVersion4(), Uuid.CreateVersion4()];
        Uuid[][] locations = [[Uuid.CreateVersion4(), Uuid.CreateVersion4()], [Uuid.CreateVersion4(), Uuid.CreateVersion4()]];
        Uuid[][] processes = [[Uuid.CreateVersion4(), Uuid.CreateVersion4()], [Uuid.CreateVersion4(), Uuid.CreateVersion4()]];
        foreach (var t in new[] { 0, 1 })
        {
            await using var batch = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenants[t]), ProjectionCheckpoint.Start));
            for (var i = 0; i < 2; i++)
            {
                await directory.ApplyAsync(new LocationRevisionRecorded(tenants[t], locations[t][i], 1,
                    Loc($"Site {i}"), Author, Now));
                await directory.ApplyAsync(new LocationRevisionRecorded(tenants[t], locations[t][i], 2,
                    Loc($"Site {i} renamed"), Author, Now.AddMinutes(1)));
                await directory.ApplyAsync(new OperationalProcessRevisionRecorded(tenants[t], processes[t][i], 1,
                    Proc($"Process {i}"), Author, Now));
            }
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var seenLocations = new List<LocationView>();
        var seenProcesses = new List<OperationalProcessView>();
        string? cursor = null;
        do
        {
            var page = await directory.ListLocationsAsync(tenants[0], 1, cursor);
            seenLocations.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);
        do
        {
            var page = await directory.ListOperationalProcessesAsync(tenants[0], 1, cursor);
            seenProcesses.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);
        var history = await directory.ListLocationRevisionsAsync(tenants[0], locations[0][0], 10, null);
        var firstPage = await directory.ListLocationsAsync(tenants[0], 1, null);

        // Assert
        Assert.Equal(locations[0].Order(), seenLocations.Select(static view => view.LocationId).Order());
        Assert.All(seenLocations, view => { Assert.Equal(tenants[0], view.TenantId); Assert.Equal(2, view.Revision); });
        Assert.Equal(processes[0].Order(), seenProcesses.Select(static view => view.OperationalProcessId).Order());
        Assert.All(seenProcesses, view => Assert.Equal(tenants[0], view.TenantId));
        Assert.Equal([1L, 2L], history.Items.Select(static view => view.Revision));
        Assert.Equal("manual", history.Items[0].SourceKind);
        Assert.Null(await directory.GetLocationAsync(tenants[1], locations[0][0]));
        Assert.Null(await directory.GetOperationalProcessAsync(tenants[1], processes[0][0]));
        Assert.Empty((await directory.ListLocationRevisionsAsync(tenants[1], locations[0][0], 10, null)).Items);
        await Assert.ThrowsAsync<KvDirectoryQueryException>(async () =>
            await directory.ListLocationsAsync(tenants[1], 1, firstPage.NextCursor));
    }

    [Fact]
    public async Task ShouldRetainHistoryAndRejectChangedReplayGivenCommittedRevisions()
    {
        // Arrange
        var directory = new FitzInventoryRegisterDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var first = new LocationRevisionRecorded(tenantId, id, 1, Loc("Original"), Author, Now);
        var second = new LocationRevisionRecorded(tenantId, id, 2, Loc("Revised"), Author, Now.AddMinutes(1));
        await using (var initial = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(first);
            await directory.ApplyAsync(second);
            await initial.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        await using (var replay = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(first);
            await directory.ApplyAsync(second);
            await replay.CommitAsync(ProjectionCheckpoint.Start);
        }
        await using (var changed = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await directory.ApplyAsync(
                first with { Content = first.Content with { Name = "Replacement" } }));

        // Assert
        Assert.Equal("Revised", (await directory.GetLocationAsync(tenantId, id))!.Content.Name);
        Assert.Equal(2, (await directory.ListLocationRevisionsAsync(tenantId, id, 10, null)).Items.Count);
    }

    [Fact]
    public async Task ShouldRejectBeforeCommitGivenRevisionWithoutPredecessor()
    {
        // Arrange
        var directory = new FitzInventoryRegisterDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start));

        // Act
        var gap = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyAsync(new OperationalProcessRevisionRecorded(tenantId,
                Uuid.CreateVersion4(), 2, Proc("Orphan"), Author, Now)));

        // Assert
        Assert.Contains("predecessor", gap.Message, StringComparison.Ordinal);
    }

    static LocationContent Loc(string name) => new("physical_site", name, null, Owner, "active");

    static OperationalProcessContent Proc(string name) =>
        new(name, "Purpose", Owner, null, null, "active");

    static CheckpointIdentity Identity(Uuid tenantId) => new(FitzInventoryRegisterDirectory.ProjectorName,
        EventStreamPattern.ForPattern(tenantId.ToString(), "inventory-registers"));
}
