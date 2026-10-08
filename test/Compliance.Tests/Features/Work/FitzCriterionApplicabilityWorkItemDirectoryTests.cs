using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FitzCriterionApplicabilityWorkItemDirectoryTests
{
    [Fact]
    public async Task ShouldRejectSkippedDecisionRevisionGivenProjectedHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var decisionId = Uuid.CreateVersion4();
        var editionId = Uuid.CreateVersion4();
        var proposerId = Uuid.CreateVersion4();
        var proposer = ActorReference.ForMember(proposerId, "Proposer");
        var proposedAt = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var directory = new FitzCriterionApplicabilityWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(
            FitzCriterionApplicabilityWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "criterion-applicability"));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(Proposed(tenantId, programId, decisionId, editionId,
            proposerId, proposer, 1, 1, proposedAt));

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyAsync(Reviewed(tenantId, programId, decisionId,
                ActorReference.ForMember(Uuid.CreateVersion4(), "Reviewer"), 3, 1, "reject",
                proposedAt.AddMinutes(1))));

        // Assert
        Assert.Contains("advance its existing source revision", failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldProjectOneReviewPerPendingVersionGivenDecisionLifecycle()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var decisionId = Uuid.CreateVersion4();
        var editionId = Uuid.CreateVersion4();
        var proposerId = Uuid.CreateVersion4();
        var reviewerId = Uuid.CreateVersion4();
        var proposer = ActorReference.ForMember(proposerId, "Proposer");
        var reviewer = ActorReference.ForMember(reviewerId, "Reviewer");
        var proposedAt = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var directory = new FitzCriterionApplicabilityWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(
            FitzCriterionApplicabilityWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "criterion-applicability"));
        var firstCheckpoint = new ProjectionCheckpoint(new EventCursor("applicability-version-one"));
        var secondCheckpoint = new ProjectionCheckpoint(new EventCursor("applicability-version-two"));
        var reviewedCheckpoint = new ProjectionCheckpoint(new EventCursor("applicability-reviewed"));
        var withdrawnCheckpoint = new ProjectionCheckpoint(new EventCursor("applicability-withdrawn"));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(Proposed(tenantId, programId, decisionId, editionId,
                proposerId, proposer, 1, 1, proposedAt));
            await batch.CommitAsync(firstCheckpoint);
        }
        var firstProposal = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         firstCheckpoint)))
        {
            await directory.ApplyAsync(Reviewed(tenantId, programId, decisionId, reviewer,
                2, 1, "reject", proposedAt.AddMinutes(1)));
            await directory.ApplyAsync(Proposed(tenantId, programId, decisionId, editionId,
                proposerId, proposer, 3, 2, proposedAt.AddMinutes(2)));
            await batch.CommitAsync(secondCheckpoint);
        }
        var secondProposal = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         secondCheckpoint)))
        {
            await directory.ApplyAsync(Reviewed(tenantId, programId, decisionId, reviewer,
                4, 2, "accept", proposedAt.AddMinutes(3)));
            await batch.CommitAsync(reviewedCheckpoint);
        }
        var accepted = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         reviewedCheckpoint)))
        {
            await directory.ApplyAsync(new CriterionNotApplicableWithdrawn(tenantId, programId,
                decisionId, 5, 2, "The criterion applies now.", proposer,
                proposedAt.AddMinutes(4)));
            await batch.CommitAsync(withdrawnCheckpoint);
        }
        var withdrawn = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);

        // Assert
        Assert.True(firstProposal.IsSuccess);
        var first = Assert.Single(firstProposal.Value);
        Assert.Equal(WorkSource.CriterionApplicabilityReview, first.Kind);
        Assert.Equal(decisionId, first.SourceId);
        Assert.Equal("Review not-applicable proposal for AC-2.4", first.Summary);
        Assert.Equal("A criterion not-applicable proposal is awaiting independent review.",
            first.Reason);
        Assert.Equal("review", first.NextAction);
        Assert.Equal($"/api/v1/tenants/{tenantId}/programs/{programId}/" +
            $"criterion-applicability/{decisionId}/reviews", first.ActionPath);
        Assert.Equal(new OperatingHolder(OperatingAuthority.ProgramManagerHolder, programId),
            first.Responsible);
        Assert.Contains(proposerId, first.Excluded);
        Assert.Equal(proposedAt, first.CreatedAt);
        Assert.Equal(WorkCandidate.IdFor(Uuid.CreateVersion5(decisionId,
            "criterion-applicability-review\n1"), WorkSource.CriterionApplicabilityReview),
            first.WorkItemId);

        Assert.True(secondProposal.IsSuccess);
        var second = Assert.Single(secondProposal.Value);
        Assert.NotEqual(first.WorkItemId, second.WorkItemId);
        Assert.Contains(proposerId, second.Excluded);
        Assert.Empty(accepted.Value);
        Assert.Empty(withdrawn.Value);
        Assert.Equal(5, await directory.LoadRevisionAsync(tenantId));
        Assert.Equal(withdrawnCheckpoint, await directory.LoadCheckpointAsync(tenantId));
    }

    static CriterionNotApplicableProposed Proposed(Uuid tenantId, Uuid programId,
        Uuid decisionId, Uuid editionId, Uuid proposerId, ActorReference proposer,
        long revision, int versionNumber, DateTimeOffset proposedAt) =>
        new(tenantId, programId, decisionId, revision, versionNumber, editionId,
            "AC-2.4", "The control is not relevant to this organization.", proposerId,
            proposer, proposedAt);

    static CriterionApplicabilityReviewed Reviewed(Uuid tenantId, Uuid programId,
        Uuid decisionId, ActorReference reviewer, long revision, int versionNumber,
        string outcome, DateTimeOffset reviewedAt) =>
        new(tenantId, programId, decisionId, revision, versionNumber, Uuid.CreateVersion4(),
            outcome, "Reviewed the applicability rationale.", reviewer, reviewedAt, null);
}
