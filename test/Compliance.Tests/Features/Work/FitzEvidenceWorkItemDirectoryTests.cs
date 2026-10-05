using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FitzEvidenceWorkItemDirectoryTests
{
    [Fact]
    public async Task ShouldProjectEvidenceWorkGivenRequestOpenedEvent()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var openedAt = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var dueOn = new DateOnly(2026, 10, 9);
        var directory = new FitzEvidenceWorkItemDirectory(new InMemoryKvClient());
        var checkpoint = new ProjectionCheckpoint(new EventCursor("evidence-cursor"));
        var identity = new CheckpointIdentity(FitzEvidenceWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "evidence-requests"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new EvidenceRequestOpened(tenantId, programId, requestId,
                "Q3 access export", "Upload the signed access export.", ownerId, dueOn,
                controlId, ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"), openedAt));
            await batch.CommitAsync(checkpoint);
        }

        var result = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var candidate = Assert.Single(result.Value);
        Assert.Equal(WorkCandidate.IdFor(requestId, "evidence_request"), candidate.WorkItemId);
        Assert.Equal("evidence_request", candidate.Kind);
        Assert.Equal(requestId, candidate.SourceId);
        Assert.Equal(controlId, candidate.ControlId);
        Assert.Equal("Q3 access export", candidate.Summary);
        Assert.Equal("Upload the signed access export.", candidate.Reason);
        Assert.Equal(dueOn, candidate.DueOn);
        Assert.Equal("fulfil", candidate.NextAction);
        Assert.Equal(new OperatingHolder(OperatingAuthority.MemberHolder, ownerId),
            candidate.Responsible);
        Assert.Equal(openedAt, candidate.CreatedAt);
        Assert.Equal(1, await directory.LoadRevisionAsync(tenantId));
        Assert.Equal(checkpoint, await directory.LoadCheckpointAsync(tenantId));
    }

    [Fact]
    public async Task ShouldRemoveEvidenceWorkGivenRequestFulfilledOrCancelled()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var fulfilledId = Uuid.CreateVersion4();
        var cancelledId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var directory = new FitzEvidenceWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzEvidenceWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "evidence-requests"));
        var fulfilled = new EvidenceRequestOpened(tenantId, programId, fulfilledId, "Export",
            "Upload export.", Uuid.CreateVersion4(), new DateOnly(2026, 10, 9), null, actor, now);
        var cancelled = new EvidenceRequestOpened(tenantId, programId, cancelledId, "Roster",
            "Upload roster.", Uuid.CreateVersion4(), new DateOnly(2026, 10, 9), null, actor, now);

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(fulfilled);
            await directory.ApplyAsync(cancelled);
            await directory.ApplyAsync(new EvidenceRequestFulfilled(tenantId, programId,
                fulfilledId, 2, Uuid.CreateVersion4(), actor, now.AddMinutes(1)));
            await directory.ApplyAsync(new EvidenceRequestCancelled(tenantId, programId,
                cancelledId, 2, "No longer required.", actor, now.AddMinutes(2)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        var result = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Equal(4, await directory.LoadRevisionAsync(tenantId));
    }

    [Fact]
    public async Task ShouldKeepEvidenceWorkTenantScopedGivenSharedKvClient()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var directory = new FitzEvidenceWorkItemDirectory(client);
        var identity = new CheckpointIdentity(FitzEvidenceWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "evidence-requests"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new EvidenceRequestOpened(tenantId, programId, requestId,
                "Export", "Upload export.", Uuid.CreateVersion4(), new DateOnly(2026, 10, 9),
                null, ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"), now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        var ownerResult = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        var otherTenantResult = await directory.LoadProgramAsync(otherTenantId, programId,
            CancellationToken.None);

        // Assert
        Assert.True(ownerResult.IsSuccess);
        Assert.Single(ownerResult.Value);
        Assert.True(otherTenantResult.IsSuccess);
        Assert.Empty(otherTenantResult.Value);
    }

    [Fact]
    public async Task ShouldRejectCompletionBeforeProjectedOpenGivenBrokenHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var directory = new FitzEvidenceWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzEvidenceWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "evidence-requests"));
        var fulfilled = new EvidenceRequestFulfilled(tenantId, programId, requestId, 2,
            Uuid.CreateVersion4(), ActorReference.ForMember(Uuid.CreateVersion4(), "Owner"),
            new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(fulfilled));

        var result = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Equal(0, await directory.LoadRevisionAsync(tenantId));
    }
}
