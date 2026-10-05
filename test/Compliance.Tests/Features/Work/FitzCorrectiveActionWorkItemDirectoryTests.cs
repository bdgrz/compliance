using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FitzCorrectiveActionWorkItemDirectoryTests
{
    [Fact]
    public async Task ShouldProjectCorrectiveActionAndRefreshMaterialityGivenFindingRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var actionId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var dueOn = new DateOnly(2026, 10, 9);
        var directory = new FitzCorrectiveActionWorkItemDirectory(new InMemoryKvClient());
        var checkpoint = new ProjectionCheckpoint(new EventCursor("remediation-cursor"));
        var identity = new CheckpointIdentity(FitzCorrectiveActionWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "remediation"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(FindingRaisedEvent(tenantId, programId, findingId,
                "high", actor, now));
            await directory.ApplyAsync(new CorrectiveActionAdded(tenantId, programId, findingId, 2,
                new CorrectiveActionView(actionId, "Remove legacy access", ownerId, dueOn,
                    RemediationLedger.Open, actor, now.AddMinutes(1))));
            await directory.ApplyAsync(new FindingRevised(tenantId, programId, findingId, 3,
                "medium", ownerId, dueOn, "Production environment", null,
                "Severity was reassessed.", actor, now.AddMinutes(2)));
            await batch.CommitAsync(checkpoint);
        }

        var result = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var candidate = Assert.Single(result.Value);
        Assert.Equal(WorkCandidate.IdFor(actionId, WorkSource.CorrectiveAction),
            candidate.WorkItemId);
        Assert.Equal(WorkSource.CorrectiveAction, candidate.Kind);
        Assert.Equal(actionId, candidate.SourceId);
        Assert.Equal(findingId, candidate.FindingId);
        Assert.Equal("Remove legacy access", candidate.Summary);
        Assert.Equal("Corrective action for finding \"Unauthorized access\".", candidate.Reason);
        Assert.Equal(dueOn, candidate.DueOn);
        Assert.Equal(RemediationLedger.Materiality("medium"), candidate.Materiality);
        Assert.Equal("complete", candidate.NextAction);
        Assert.EndsWith($"/findings/{findingId}/corrective-actions/{actionId}/completions",
            candidate.ActionPath, StringComparison.Ordinal);
        Assert.Equal(new OperatingHolder(OperatingAuthority.MemberHolder, ownerId),
            candidate.Responsible);
        Assert.Equal(now.AddMinutes(1), candidate.CreatedAt);
        Assert.Equal(3, await directory.LoadRevisionAsync(tenantId));
        Assert.Equal(checkpoint, await directory.LoadCheckpointAsync(tenantId));
    }

    [Fact]
    public async Task ShouldRemoveCorrectiveActionGivenCompletionEvent()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var actionId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var directory = new FitzCorrectiveActionWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzCorrectiveActionWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "remediation"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(FindingRaisedEvent(tenantId, programId, findingId,
                "high", actor, now));
            await directory.ApplyAsync(new CorrectiveActionAdded(tenantId, programId, findingId, 2,
                new CorrectiveActionView(actionId, "Remove legacy access", ownerId,
                    new DateOnly(2026, 10, 9), RemediationLedger.Open, actor, now.AddMinutes(1))));
            await directory.ApplyAsync(new CorrectiveActionCompleted(tenantId, programId, findingId,
                3, actionId, "Removed.", [], ownerId, actor, now.AddMinutes(2)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        var result = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Equal(3, await directory.LoadRevisionAsync(tenantId));
    }

    [Fact]
    public async Task ShouldRejectActionBeforeFindingGivenBrokenHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var directory = new FitzCorrectiveActionWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzCorrectiveActionWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "remediation"));
        var action = new CorrectiveActionAdded(tenantId, programId, findingId, 2,
            new CorrectiveActionView(Uuid.CreateVersion4(), "Remove legacy access",
                Uuid.CreateVersion4(), new DateOnly(2026, 10, 9), RemediationLedger.Open,
                actor, DateTimeOffset.UtcNow));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(action));

        var result = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Equal(0, await directory.LoadRevisionAsync(tenantId));
    }

    [Fact]
    public async Task ShouldKeepCorrectiveActionWorkTenantScopedGivenSharedKvClient()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var actionId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var directory = new FitzCorrectiveActionWorkItemDirectory(client);
        var identity = new CheckpointIdentity(FitzCorrectiveActionWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "remediation"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(FindingRaisedEvent(tenantId, programId, findingId,
                "high", actor, now));
            await directory.ApplyAsync(new CorrectiveActionAdded(tenantId, programId, findingId, 2,
                new CorrectiveActionView(actionId, "Remove legacy access", ownerId,
                    new DateOnly(2026, 10, 9), RemediationLedger.Open, actor, now.AddMinutes(1))));
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

    static FindingRaised FindingRaisedEvent(Uuid tenantId, Uuid programId, Uuid findingId,
        string severity, ActorReference actor, DateTimeOffset at) =>
        new(tenantId, programId, findingId, 1,
            new FindingSource("manual", null, null, "Manually recorded finding."),
            "Unauthorized access", "A former contractor retained access.", severity,
            "Production environment", Uuid.CreateVersion4(), new DateOnly(2026, 10, 9), [],
            actor, at);
}
