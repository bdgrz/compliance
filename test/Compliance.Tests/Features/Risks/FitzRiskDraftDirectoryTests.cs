using Bdgrz.Compliance.Features.Risks;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class FitzRiskDraftDirectoryTests
{
    [Fact]
    public async Task ShouldKeepTenantAndProgramPagesSeparateGivenSharedProjection()
    {
        // Arrange
        var directory = new FitzRiskDraftDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var otherProgramId = Uuid.CreateVersion4();
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            foreach (var index in Enumerable.Range(1, 201))
                await directory.ApplyAsync(Created(tenantId, programId, $"R-{index:D3}"));
            await directory.ApplyAsync(Created(tenantId, otherProgramId, "OTHER"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var first = await directory.ListProgramAsync(tenantId, programId, 200, null);
        var second = await directory.ListProgramAsync(tenantId, programId, 200,
            first.NextCursor);
        var alien = await directory.ListProgramAsync(otherTenantId, programId, 200, null);

        // Assert
        Assert.Equal(200, first.Items.Count);
        Assert.Single(second.Items);
        Assert.Empty(alien.Items);
        Assert.All(first.Items.Concat(second.Items),
            item => Assert.Equal("draft_unassessed", item.Status));
    }

    [Fact]
    public async Task ShouldRollBackCurrentAndHistoryGivenInvalidRevision()
    {
        // Arrange
        var directory = new FitzRiskDraftDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        RiskDraftCreated created = new(tenantId, programId,
            RiskDraft.IdFor(tenantId, programId, "R-01"), Uuid.CreateVersion4(),
            "R-01", new RiskDraftContent("Provider outage", "Provider unavailable",
                "Service outage", "Management observation"), Uuid.CreateVersion4(),
            "Author", new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
        RiskDraftRevised invalid = new(tenantId, programId, created.RiskId, 3,
            new RiskDraftContent("Provider outage", "Changed", "Service outage",
                "Management observation"), created.ActorMemberId,
            "Editor", created.ChangedAt.AddMinutes(1));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(invalid));
        }
        var current = await directory.GetAsync(tenantId, created.RiskId);
        var history = await directory.GetRevisionAsync(tenantId, created.RiskId, 1);
        var checkpoint = await directory.LoadCheckpointAsync(Identity(tenantId));
        await using (var retry = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await retry.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Null(current);
        Assert.Null(history);
        Assert.Equal(ProjectionCheckpoint.Start, checkpoint);
        Assert.Equal(1, (await directory.GetAsync(tenantId, created.RiskId))?.Revision);
    }

    static CheckpointIdentity Identity(Uuid tenantId) => new("RiskDraftDirectory",
        EventStreamPattern.ForPattern(tenantId.ToString(), "risks"));

    static RiskDraftCreated Created(Uuid tenantId, Uuid programId, string identifier) =>
        new(tenantId, programId, RiskDraft.IdFor(tenantId, programId, identifier),
            Uuid.CreateVersion4(), identifier,
            new RiskDraftContent("Provider outage", "Provider unavailable",
                "Service outage", "Management observation"), Uuid.CreateVersion4(),
            "Author", new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
}
