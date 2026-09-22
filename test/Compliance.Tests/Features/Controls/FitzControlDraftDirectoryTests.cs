using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class FitzControlDraftDirectoryV2Tests
{
    [Fact]
    public async Task ShouldUseIndependentV2CheckpointGivenCurrentControlDraftProjection()
    {
        // Arrange
        var directory = new FitzControlDraftDirectoryV2(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var checkpoint = new ProjectionCheckpoint(new EventCursor("control-draft-v2"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(Checkpoint(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(Created(tenantId, Uuid.CreateVersion4(), "AC-V2",
                new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero)));
            await batch.CommitAsync(checkpoint);
        }

        // Assert
        Assert.Equal(checkpoint, await directory.LoadCheckpointAsync(tenantId));
    }

    [Fact]
    public async Task ShouldPageProgramDraftsWithoutDisclosingOtherProgramsOrTenantsGivenSharedDirectory()
    {
        // Arrange
        var directory = new FitzControlDraftDirectoryV2(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var otherProgramId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var expected = Enumerable.Range(1, 201)
            .Select(index => $"AC-{index:D3}").ToArray();
        var otherControlId = ControlDraft.IdFor(tenantId, otherProgramId, "OTHER");
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(Checkpoint(tenantId), ProjectionCheckpoint.Start)))
        {
            foreach (var identifier in expected)
                await directory.ApplyAsync(Created(tenantId, programId, identifier, now));
            await directory.ApplyAsync(Created(tenantId, otherProgramId, "OTHER", now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var first = await directory.ListProgramAsync(tenantId, programId, 200, null);
        var second = await directory.ListProgramAsync(tenantId, programId, 200,
            first.NextCursor);
        var otherProgram = await directory.ListProgramAsync(tenantId, otherProgramId, 200,
            null);
        var alienProgram = await directory.ListProgramAsync(otherTenantId, programId, 200,
            null);

        // Assert
        Assert.Equal(200, first.Items.Count);
        Assert.Single(second.Items);
        Assert.Equal(expected, first.Items.Concat(second.Items).Select(item => item.Identifier));
        Assert.Equal("OTHER", Assert.Single(otherProgram.Items).Identifier);
        Assert.Empty(alienProgram.Items);
        Assert.Null(await directory.GetAsync(otherTenantId, otherControlId));
        Assert.Null(await directory.GetRevisionAsync(otherTenantId, otherControlId, 1));
    }

    [Fact]
    public async Task ShouldPreserveRevisionHistoryAndAttributionGivenCommittedProjection()
    {
        // Arrange
        var directory = new FitzControlDraftDirectoryV2(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        ControlDraftCreated created = new(tenantId, programId,
            ControlDraft.IdFor(tenantId, programId, "AC-01"), Uuid.CreateVersion4(),
            "AC-01", Content(), Uuid.CreateVersion4(), "Author",
            new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
        var nextActorId = Uuid.CreateVersion4();
        ControlDraftRevised revised = new(tenantId, programId, created.ControlId, 2,
            Content() with { Title = "Revised" }, nextActorId, "Reviewer",
            created.ChangedAt.AddMinutes(1));
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(Checkpoint(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await directory.ApplyAsync(revised);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var current = await directory.GetAsync(tenantId, created.ControlId);
        var first = await directory.GetRevisionAsync(tenantId, created.ControlId, 1);
        var second = await directory.GetRevisionAsync(tenantId, created.ControlId, 2);

        // Assert
        Assert.Equal(2, current?.Revision);
        Assert.Equal("draft", current?.Status);
        Assert.Equal("unresolved", current?.OwnerResolution);
        Assert.Equal("unresolved", current?.ApplicabilityResolution);
        Assert.Equal("Revised", current?.Content.Title);
        Assert.Equal(nextActorId, current?.LastChangedByMemberId);
        Assert.Equal(created.ActorMemberId, first?.ChangedByMemberId);
        Assert.Equal("Access review", first?.Content.Title);
        Assert.Equal(nextActorId, second?.ChangedByMemberId);
        Assert.Equal("Revised", second?.Content.Title);
    }

    [Fact]
    public async Task ShouldRollBackDraftAndCheckpointGivenInvalidProjectionRevision()
    {
        // Arrange
        var directory = new FitzControlDraftDirectoryV2(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var identity = Checkpoint(tenantId);
        ControlDraftCreated created = new(tenantId, programId,
            ControlDraft.IdFor(tenantId, programId, "AC-01"), Uuid.CreateVersion4(),
            "AC-01", Content(), Uuid.CreateVersion4(), "Author",
            new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
        ControlDraftRevised invalid = new(tenantId, programId, created.ControlId, 3,
            Content(), created.ActorMemberId, "Author", created.ChangedAt.AddMinutes(1));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(invalid));
        }
        var missing = await directory.GetAsync(tenantId, created.ControlId);
        var missingRevision = await directory.GetRevisionAsync(tenantId, created.ControlId, 1);
        var checkpoint = await directory.LoadCheckpointAsync(identity);
        await using (var retry = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await retry.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Null(missing);
        Assert.Null(missingRevision);
        Assert.Equal(ProjectionCheckpoint.Start, checkpoint);
        Assert.Equal(1, (await directory.GetAsync(tenantId, created.ControlId))?.Revision);
    }

    [Fact]
    public async Task ShouldRemoveCurrentDraftGivenDiscardOfUnpublishedControl()
    {
        // Arrange
        var directory = new FitzControlDraftDirectoryV2(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
        var created = Created(tenantId, programId, "AC-06", now);

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(Checkpoint(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await directory.ApplyAsync(new ControlDraftDiscarded(tenantId, programId,
                created.ControlId, 1, created.ActorMemberId, "Author", "Not needed",
                now.AddMinutes(1)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Null(await directory.GetAsync(tenantId, created.ControlId));
        Assert.Empty((await directory.ListProgramAsync(tenantId, programId, 20, null)).Items);
    }

    static CheckpointIdentity Checkpoint(Uuid tenantId) => new(
        "ControlDraftDirectoryV2", EventStreamPattern.ForPattern(tenantId.ToString(), "controls"));

    static ControlDraftCreated Created(Uuid tenantId, Uuid programId, string identifier,
        DateTimeOffset now) => new(tenantId, programId,
        ControlDraft.IdFor(tenantId, programId, identifier), Uuid.CreateVersion4(),
        identifier, Content(), Uuid.CreateVersion4(), "Author", now);

    static ControlDraftContent Content() => new("Access review", "Review access",
        "Management reviews access", "The owner reviews the access list quarterly.",
        ["Dated review record"]);
}
