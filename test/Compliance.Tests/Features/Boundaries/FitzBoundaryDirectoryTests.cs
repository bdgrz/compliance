using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Boundaries;

public sealed class FitzBoundaryDirectoryTests
{
    [Fact]
    public async Task ShouldWaitForExactProjectedRevisionGivenImpactPreview()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var draftId = Uuid.CreateVersion4();
        var authorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var content = new BoundaryContent("Initial", "readiness", ["security"], []);
        var directory = new FitzBoundaryDirectory(new InMemoryKvClient());
        var impact = new BoundaryImpactService(directory,
            [new ProgramBoundaryImpactContributor()]);
        var identity = new CheckpointIdentity("BoundaryDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));

        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new BoundaryDraftCreated(tenantId, boundaryId,
                programId, draftId, content, authorId, "Author", now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        var requested = new PreviewBoundaryImpact(tenantId, boundaryId, draftId, 2);

        // Act
        var lagged = await impact.PreviewAsync(requested, CancellationToken.None);

        // Assert
        Assert.False(lagged.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, lagged.Error.Kind);

        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new BoundaryDraftRevised(tenantId, boundaryId,
                draftId, 2, content with { Statement = "Revised" }, authorId,
                "Author", now.AddMinutes(1)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        var caughtUp = await impact.PreviewAsync(requested, CancellationToken.None);
        Assert.True(caughtUp.IsSuccess);
        Assert.True(caughtUp.Value.Complete);
        Assert.Contains(caughtUp.Value.Changes, change =>
            change.Field == "statement" && change.ProposedValue == "Revised");
    }

    [Fact]
    public async Task ShouldRollBackAndRetryGivenFailedProjectionBatch()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var draftId = Uuid.CreateVersion4();
        var authorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var content = new BoundaryContent("Original", "readiness", ["security"], []);
        var directory = new FitzBoundaryDirectory(new InMemoryKvClient());

        // Act
        var identity = new CheckpointIdentity("BoundaryDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));


        // Assert
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new BoundaryDraftCreated(tenantId, boundaryId,
                programId, draftId, content, authorId, "Author", now));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(new BoundaryDraftRevised(tenantId, boundaryId,
                    Uuid.CreateVersion4(), 2, content with { Statement = "Revised" },
                    authorId, "Author", now.AddMinutes(1))));
        }

        Assert.Null(await directory.GetAsync(tenantId, boundaryId));

        await using (var retry = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new BoundaryDraftCreated(tenantId, boundaryId,
                programId, draftId, content, authorId, "Author", now));
            await directory.ApplyAsync(new BoundaryDraftRevised(tenantId, boundaryId,
                draftId, 2, content with { Statement = "Revised" }, authorId,
                "Author", now.AddMinutes(1)));
            await retry.CommitAsync(ProjectionCheckpoint.Start);
        }

        Assert.Equal("Revised", (await directory.GetAsync(tenantId, boundaryId))?
            .Draft?.Content.Statement);
    }

    [Fact]
    public async Task ShouldRemoveCurrentDraftGivenDiscardOfNeverReviewedVersion()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var draftId = Uuid.CreateVersion4();
        var authorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var content = new BoundaryContent("Unused", "readiness", ["security"], []);
        var directory = new FitzBoundaryDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("BoundaryDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new BoundaryDraftCreated(tenantId, boundaryId,
                programId, draftId, content, authorId, "Author", now));
            await directory.ApplyAsync(new BoundaryDraftDiscarded(tenantId, boundaryId,
                draftId, 1, authorId, "Author", "Wrong scope", now.AddMinutes(1)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Null(await directory.GetAsync(tenantId, boundaryId));
        Assert.Empty((await directory.ListProgramAsync(tenantId, programId, 20, null)).Items);
    }

    [Fact]
    public async Task ShouldPreserveEffectiveHistoryGivenApprovedSuccessor()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var originalId = Uuid.CreateVersion4();
        var successorId = Uuid.CreateVersion4();
        var authorId = Uuid.CreateVersion4();
        var reviewerId = Uuid.CreateVersion4();
        var reviewId = Uuid.CreateVersion4();
        var approvalId = Uuid.CreateVersion4();
        var nextReviewId = Uuid.CreateVersion4();
        var nextApprovalId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var original = new BoundaryContent("Original scope", "readiness", ["security"],
            [new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion", "service",
                "Service A", null, "Compliance lead", "Core service", true)]);
        var successor = original with { Statement = "Scope with provider B" };
        var directory = new FitzBoundaryDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("BoundaryDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));


        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new BoundaryDraftCreated(tenantId, boundaryId,
                programId, originalId, original, authorId, "Author", now));
            await directory.ApplyAsync(new BoundaryReviewed(tenantId, boundaryId,
                originalId, 1, reviewId, "accept", reviewerId,
                "Reviewer", "Reviewed", now.AddMinutes(1)));
            await directory.ApplyAsync(new BoundaryApproved(tenantId, boundaryId,
                originalId, 1, approvalId, reviewId, reviewerId, "Reviewer",
                "Approved", new DateOnly(2027, 1, 1), now.AddMinutes(2), "digest-1"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var predecessorBefore = await directory.GetVersionAsync(tenantId, boundaryId,
            originalId);
        var predecessorHash = SnapshotContentIdentity.ApprovedBoundaryVersion(
            Assert.IsType<BoundaryVersionView>(predecessorBefore));

        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new BoundarySuccessorProposed(tenantId, boundaryId,
                successorId, originalId, successor, authorId, "Author", now.AddDays(1)));
            await directory.ApplyAsync(new BoundaryReviewed(tenantId, boundaryId,
                successorId, 1, nextReviewId, "accept", reviewerId,
                "Reviewer", "Reviewed", now.AddDays(1).AddMinutes(1)));
            await directory.ApplyAsync(new BoundaryApproved(tenantId, boundaryId,
                successorId, 1, nextApprovalId, nextReviewId, reviewerId, "Reviewer",
                "Approved", new DateOnly(2027, 2, 1), now.AddDays(1).AddMinutes(2), "digest-2"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Null(await directory.GetEffectiveVersionAsync(tenantId, boundaryId,
            new DateOnly(2026, 12, 31)));
        Assert.Equal(originalId, (await directory.GetEffectiveVersionAsync(tenantId,
            boundaryId, new DateOnly(2027, 1, 31)))?.VersionId);
        Assert.Equal(successorId, (await directory.GetEffectiveVersionAsync(tenantId,
            boundaryId, new DateOnly(2027, 2, 1)))?.VersionId);
        Assert.Equal(successorId, (await directory.GetEffectiveVersionAsync(tenantId,
            boundaryId, new DateOnly(2028, 1, 1)))?.VersionId);
        Assert.Equal(successorId, (await directory.GetEffectiveVersionAsync(tenantId,
            boundaryId, DateOnly.MaxValue))?.VersionId);
        var predecessorAfter = await directory.GetVersionAsync(tenantId, boundaryId,
            originalId);
        Assert.Equal(predecessorHash, SnapshotContentIdentity.ApprovedBoundaryVersion(
            Assert.IsType<BoundaryVersionView>(predecessorAfter)));
        Assert.Equal([originalId, successorId],
            (await directory.ListVersionsAsync(tenantId, boundaryId, 20, null))?
            .Items.Select(item => item.VersionId));
        Assert.Equal("digest-1", (await directory.GetDecisionAsync(tenantId,
            boundaryId, approvalId))?.ImpactDigest);
        Assert.Equal(reviewId, (await directory.GetDecisionAsync(tenantId,
            boundaryId, approvalId))?.ReliesOnDecisionId);
        Assert.Null(await directory.GetVersionAsync(Uuid.CreateVersion4(),
            boundaryId, originalId));
        Assert.Null(await directory.GetDecisionAsync(Uuid.CreateVersion4(),
            boundaryId, approvalId));
    }

    [Fact]
    public async Task ShouldRejectOverlappingEffectiveDateGivenReplayedSuccessor()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var originalId = Uuid.CreateVersion4();
        var successorId = Uuid.CreateVersion4();
        var authorId = Uuid.CreateVersion4();
        var reviewerId = Uuid.CreateVersion4();
        var originalReviewId = Uuid.CreateVersion4();
        var successorReviewId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var content = new BoundaryContent("Original scope", "readiness", ["security"], []);
        var effectiveFrom = new DateOnly(2027, 1, 1);
        var directory = new FitzBoundaryDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("BoundaryDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using (var first = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new BoundaryDraftCreated(tenantId, boundaryId,
                programId, originalId, content, authorId, "Author", now));
            await directory.ApplyAsync(new BoundaryReviewed(tenantId, boundaryId,
                originalId, 1, originalReviewId, "accept", reviewerId, "Reviewer",
                "Reviewed", now));
            await directory.ApplyAsync(new BoundaryApproved(tenantId, boundaryId,
                originalId, 1, Uuid.CreateVersion4(), originalReviewId, reviewerId,
                "Reviewer", "Approved", effectiveFrom, now, "digest"));
            await first.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        await using (var successor = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new BoundarySuccessorProposed(tenantId,
                boundaryId, successorId, originalId, content, authorId, "Author", now));
            await directory.ApplyAsync(new BoundaryReviewed(tenantId, boundaryId,
                successorId, 1, successorReviewId, "accept", reviewerId, "Reviewer",
                "Reviewed", now));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(new BoundaryApproved(tenantId, boundaryId,
                    successorId, 1, Uuid.CreateVersion4(), successorReviewId,
                    reviewerId, "Reviewer", "Approved", effectiveFrom, now, "digest")));
        }

        // Assert
        Assert.Null(await directory.GetVersionAsync(tenantId, boundaryId, successorId));
        Assert.Equal(originalId, (await directory.GetEffectiveVersionAsync(tenantId,
            boundaryId, effectiveFrom))?.VersionId);
    }

    [Fact]
    public async Task ShouldSelectEffectiveVersionGivenHistoryAcrossProjectionPages()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var authorId = Uuid.CreateVersion4();
        var reviewerId = Uuid.CreateVersion4();
        var firstDate = new DateOnly(2027, 1, 1);
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var content = new BoundaryContent("Scope", "readiness", ["security"], []);
        var directory = new FitzBoundaryDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("BoundaryDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        var versionIds = new List<Uuid>();

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            Uuid? predecessorId = null;
            for (var index = 0; index < 201; index++)
            {
                var versionId = Uuid.CreateVersion4();
                var reviewId = Uuid.CreateVersion4();
                versionIds.Add(versionId);
                if (predecessorId is { } prior)
                    await directory.ApplyAsync(new BoundarySuccessorProposed(tenantId,
                        boundaryId, versionId, prior, content, authorId, "Author", now));
                else
                    await directory.ApplyAsync(new BoundaryDraftCreated(tenantId,
                        boundaryId, programId, versionId, content, authorId, "Author", now));
                await directory.ApplyAsync(new BoundaryReviewed(tenantId, boundaryId,
                    versionId, 1, reviewId, "accept", reviewerId, "Reviewer", "Reviewed", now));
                await directory.ApplyAsync(new BoundaryApproved(tenantId, boundaryId,
                    versionId, 1, Uuid.CreateVersion4(), reviewId, reviewerId, "Reviewer",
                    "Approved", firstDate.AddDays(index), now, "digest"));
                predecessorId = versionId;
            }
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var firstPage = await directory.ListVersionsAsync(tenantId, boundaryId, 200, null);
        var nextPage = await directory.ListVersionsAsync(tenantId, boundaryId, 200,
            firstPage?.NextCursor);

        // Assert
        Assert.Null(await directory.GetEffectiveVersionAsync(tenantId, boundaryId,
            firstDate.AddDays(-1)));
        Assert.Equal(versionIds[199], (await directory.GetEffectiveVersionAsync(tenantId,
            boundaryId, firstDate.AddDays(199)))?.VersionId);
        Assert.Equal(versionIds[200], (await directory.GetEffectiveVersionAsync(tenantId,
            boundaryId, firstDate.AddDays(200)))?.VersionId);
        Assert.Equal(200, firstPage?.Items.Count);
        Assert.Single(Assert.IsType<Page<BoundaryVersionView>>(nextPage).Items);
    }
}
