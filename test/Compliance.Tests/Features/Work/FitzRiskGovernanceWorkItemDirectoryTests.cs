using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FitzRiskGovernanceWorkItemDirectoryTests
{
    [Fact]
    public async Task ShouldProjectActionCompletionReviewGivenOpenActionRevisionAndSubmission()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var riskId = Uuid.CreateVersion4();
        var actionId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var submitterId = Uuid.CreateVersion4();
        var submissionId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var now = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var dueOn = new DateOnly(2026, 10, 12);
        var directory = new FitzRiskGovernanceWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzRiskGovernanceWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "risk-governance"));
        var checkpoint = new ProjectionCheckpoint(new EventCursor("risk-governance-cursor"));

        Assert.Equal(new[]
        {
            WorkSource.RiskTreatmentAction,
            WorkSource.RiskTreatmentActionReview,
            WorkSource.RiskControlTreatmentReview,
        }, directory.ProjectedKinds);

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(ActionAdded(tenantId, programId, riskId, actionId,
                ownerId, actor, now));
            await directory.ApplyAsync(new RiskTreatmentActionRevised(tenantId, programId,
                riskId, 2, actionId, Uuid.CreateVersion4(), 1, "Enforce MFA consistently",
                "Every administrator must use MFA.", "Identity provider policy export.", dueOn,
                ownerId, [], actor, now.AddMinutes(1)));
            await directory.ApplyAsync(new RiskTreatmentActionCompletionSubmitted(tenantId,
                programId, riskId, 3, actionId, submissionId,
                "MFA is enabled for all administrators.", [Uuid.CreateVersion4()], submitterId,
                actor, now.AddMinutes(2)));
            await batch.CommitAsync(checkpoint);
        }

        var result = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var review = Assert.Single(result.Value);
        Assert.Equal(WorkSource.RiskTreatmentActionReview, review.Kind);
        Assert.Equal(WorkSource.RiskTreatmentWorkItemId(programId, riskId, submissionId,
            WorkSource.RiskTreatmentActionReview), review.WorkItemId);
        Assert.Equal(submissionId, review.SourceId);
        Assert.Equal("Review completion for Enforce MFA consistently", review.Summary);
        Assert.Equal("A risk treatment action completion is awaiting independent review.",
            review.Reason);
        Assert.Equal(dueOn, review.DueOn);
        Assert.Equal("review", review.NextAction);
        Assert.Equal($"/api/v1/tenants/{tenantId}/programs/{programId}/" +
            $"risks/{riskId}/treatment-actions/{actionId}/completion-reviews", review.ActionPath);
        Assert.Equal(new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, programId),
            review.Responsible);
        Assert.Equal(new HashSet<Uuid> { ownerId, submitterId }, review.Excluded);
        Assert.Equal(now.AddMinutes(2), review.CreatedAt);
        Assert.Equal(3, await directory.LoadRevisionAsync(tenantId));
        Assert.Equal(checkpoint, await directory.LoadCheckpointAsync(tenantId));
    }

    [Fact]
    public async Task ShouldRestoreActionAfterRejectedReviewGivenAcceptedAndRejectedReviews()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var riskId = Uuid.CreateVersion4();
        var actionId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var submitterId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var now = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var firstSubmissionId = Uuid.CreateVersion4();
        var secondSubmissionId = Uuid.CreateVersion4();
        var directory = new FitzRiskGovernanceWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzRiskGovernanceWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "risk-governance"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(ActionAdded(tenantId, programId, riskId, actionId,
                ownerId, actor, now));
            await directory.ApplyAsync(CompletionSubmitted(tenantId, programId, riskId,
                actionId, firstSubmissionId, submitterId, actor, now.AddMinutes(1), 2));
            await directory.ApplyAsync(CompletionReviewed(tenantId, programId, riskId,
                actionId, RiskGovernanceLedger.Reject, actor, now.AddMinutes(2), 3));
            await batch.CommitAsync(new ProjectionCheckpoint(new EventCursor("risk-rejected")));
        }
        var reopened = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         new ProjectionCheckpoint(new EventCursor("risk-rejected")))))
        {
            await directory.ApplyAsync(CompletionSubmitted(tenantId, programId, riskId,
                actionId, secondSubmissionId, submitterId, actor, now.AddMinutes(3), 4));
            await directory.ApplyAsync(CompletionReviewed(tenantId, programId, riskId,
                actionId, RiskGovernanceLedger.Accept, actor, now.AddMinutes(4), 5));
            await batch.CommitAsync(new ProjectionCheckpoint(new EventCursor("risk-accepted")));
        }
        var completed = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);

        // Assert
        Assert.True(reopened.IsSuccess);
        var action = Assert.Single(reopened.Value);
        Assert.Equal(WorkSource.RiskTreatmentAction, action.Kind);
        Assert.Equal(actionId, action.SourceId);
        Assert.Equal(ownerId, action.Responsible.Id);
        Assert.True(completed.IsSuccess);
        Assert.Empty(completed.Value);
        Assert.Equal(5, await directory.LoadRevisionAsync(tenantId));
    }

    [Fact]
    public async Task ShouldRemovePendingControlTreatmentReviewGivenIndependentDecision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var riskId = Uuid.CreateVersion4();
        var treatmentId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var proposerId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(proposerId, "Lead");
        var now = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var directory = new FitzRiskGovernanceWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzRiskGovernanceWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "risk-governance"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new RiskControlTreatmentProposed(tenantId, programId,
                riskId, 1, new RiskControlTreatmentView(treatmentId, riskId, controlId,
                    Uuid.CreateVersion4(), "proposed", "MFA treats the risk.", actor, now,
                    null, null, null, null, null, null, null, null), proposerId));
            await batch.CommitAsync(new ProjectionCheckpoint(new EventCursor("treatment-proposed")));
        }
        var proposed = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         new ProjectionCheckpoint(new EventCursor("treatment-proposed")))))
        {
            await directory.ApplyAsync(new RiskControlTreatmentReviewed(tenantId, programId,
                riskId, 2, treatmentId, Uuid.CreateVersion4(), RiskGovernanceLedger.Accept,
                "Reviewed the exact approved control version.",
                ActorReference.ForMember(Uuid.CreateVersion4(), "Reviewer"), now.AddMinutes(1),
                null));
            await batch.CommitAsync(new ProjectionCheckpoint(new EventCursor("treatment-reviewed")));
        }
        var reviewed = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);

        // Assert
        var item = Assert.Single(proposed.Value);
        Assert.Equal(WorkSource.RiskControlTreatmentReview, item.Kind);
        Assert.Equal(treatmentId, item.SourceId);
        Assert.Equal(controlId, item.ControlId);
        Assert.Equal("review", item.NextAction);
        Assert.Equal(new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, programId),
            item.Responsible);
        Assert.Contains(proposerId, item.Excluded);
        Assert.Empty(reviewed.Value);
        Assert.Equal(2, await directory.LoadRevisionAsync(tenantId));
    }

    [Fact]
    public async Task ShouldRemoveSubmittedCompletionReviewGivenActionCancellation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var riskId = Uuid.CreateVersion4();
        var actionId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var submitterId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var now = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var directory = new FitzRiskGovernanceWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzRiskGovernanceWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "risk-governance"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(ActionAdded(tenantId, programId, riskId, actionId,
                ownerId, actor, now));
            await directory.ApplyAsync(CompletionSubmitted(tenantId, programId, riskId,
                actionId, Uuid.CreateVersion4(), submitterId, actor, now.AddMinutes(1), 2));
            await directory.ApplyAsync(new RiskTreatmentActionCancelled(tenantId, programId,
                riskId, 3, actionId, Uuid.CreateVersion4(), "The action is no longer needed.",
                actor, now.AddMinutes(2)));
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
    public async Task ShouldTrackRiskRevisionsIndependentlyGivenInterleavedActions()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var firstRiskId = Uuid.CreateVersion4();
        var secondRiskId = Uuid.CreateVersion4();
        var firstActionId = Uuid.CreateVersion4();
        var secondActionId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var now = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var directory = new FitzRiskGovernanceWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzRiskGovernanceWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "risk-governance"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(ActionAdded(tenantId, programId, firstRiskId,
                firstActionId, ownerId, actor, now));
            await directory.ApplyAsync(ActionAdded(tenantId, programId, secondRiskId,
                secondActionId, ownerId, actor, now.AddMinutes(1)));
            await directory.ApplyAsync(new RiskTreatmentActionRevised(tenantId, programId,
                firstRiskId, 2, firstActionId, Uuid.CreateVersion4(), 1,
                "Enforce MFA consistently", "Every administrator must use MFA.",
                "Identity provider policy export.", new DateOnly(2026, 10, 12), ownerId, [], actor,
                now.AddMinutes(2)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var result = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, candidate => candidate.SourceId == firstActionId &&
            candidate.Summary == "Enforce MFA consistently");
        Assert.Contains(result.Value, candidate => candidate.SourceId == secondActionId &&
            candidate.Summary == "Enforce MFA");
        Assert.Equal(3, await directory.LoadRevisionAsync(tenantId));
    }

    [Fact]
    public async Task ShouldKeepRiskActionWorkDistinctGivenSameActionIdInDifferentRisks()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var firstRiskId = Uuid.CreateVersion4();
        var secondRiskId = Uuid.CreateVersion4();
        var actionId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var now = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var source = new RiskGovernanceLedger(tenantId, programId);
        Assert.Null(source.AddTreatmentAction(firstRiskId, 0, actionId, "mitigate",
            "Enforce MFA", "MFA is required", "Identity provider policy export.",
            new DateOnly(2026, 10, 12), ownerId, [], actor, now));
        Assert.Null(source.AddTreatmentAction(secondRiskId, 0, actionId, "mitigate",
            "Enforce MFA", "MFA is required", "Identity provider policy export.",
            new DateOnly(2026, 10, 12), ownerId, [], actor, now));
        var directory = new FitzRiskGovernanceWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzRiskGovernanceWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "risk-governance"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(ActionAdded(tenantId, programId, firstRiskId, actionId,
                ownerId, actor, now));
            await directory.ApplyAsync(ActionAdded(tenantId, programId, secondRiskId, actionId,
                ownerId, actor, now));
            await batch.CommitAsync(new ProjectionCheckpoint(new EventCursor("two-risk-actions")));
        }
        var openActions = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        var submissionId = Uuid.CreateVersion4();
        var submitterId = Uuid.CreateVersion4();
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         new ProjectionCheckpoint(new EventCursor("two-risk-actions")))))
        {
            await directory.ApplyAsync(CompletionSubmitted(tenantId, programId, firstRiskId,
                actionId, submissionId, submitterId, actor, now.AddMinutes(1), 2));
            await directory.ApplyAsync(CompletionSubmitted(tenantId, programId, secondRiskId,
                actionId, submissionId, submitterId, actor, now.AddMinutes(1), 2));
            await batch.CommitAsync(new ProjectionCheckpoint(new EventCursor("two-risk-reviews")));
        }
        var reviews = await directory.LoadProgramAsync(tenantId, programId, CancellationToken.None);

        // Assert
        Assert.True(openActions.IsSuccess);
        Assert.Equal(2, openActions.Value.Count);
        Assert.Equal(2, openActions.Value.Select(candidate => candidate.WorkItemId)
            .Distinct().Count());
        Assert.All(openActions.Value, candidate =>
            Assert.Equal(WorkSource.RiskTreatmentAction, candidate.Kind));
        Assert.Contains(openActions.Value, candidate => candidate.ActionPath.Contains(
            firstRiskId.ToString(), StringComparison.Ordinal));
        Assert.Contains(openActions.Value, candidate => candidate.ActionPath.Contains(
            secondRiskId.ToString(), StringComparison.Ordinal));
        Assert.True(reviews.IsSuccess);
        Assert.Equal(2, reviews.Value.Count);
        Assert.Equal(2, reviews.Value.Select(candidate => candidate.WorkItemId)
            .Distinct().Count());
        Assert.All(reviews.Value, candidate =>
            Assert.Equal(WorkSource.RiskTreatmentActionReview, candidate.Kind));
        Assert.Contains(reviews.Value, candidate => candidate.ActionPath.Contains(
            firstRiskId.ToString(), StringComparison.Ordinal));
        Assert.Contains(reviews.Value, candidate => candidate.ActionPath.Contains(
            secondRiskId.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldKeepRiskWorkTenantScopedGivenSharedKvClient()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var riskId = Uuid.CreateVersion4();
        var actionId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var directory = new FitzRiskGovernanceWorkItemDirectory(client);
        var identity = new CheckpointIdentity(FitzRiskGovernanceWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "risk-governance"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(ActionAdded(tenantId, programId, riskId, actionId,
                Uuid.CreateVersion4(), actor, DateTimeOffset.UtcNow));
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
    public async Task ShouldRejectSkippedRiskRevisionGivenBrokenHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var riskId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var added = ActionAdded(tenantId, programId, riskId, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), actor, DateTimeOffset.UtcNow) with
        { Revision = 2 };
        var directory = new FitzRiskGovernanceWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzRiskGovernanceWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "risk-governance"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(added));

        var result = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Equal(0, await directory.LoadRevisionAsync(tenantId));
    }

    static RiskTreatmentActionAdded ActionAdded(Uuid tenantId, Uuid programId, Uuid riskId,
        Uuid actionId, Uuid ownerId, ActorReference actor, DateTimeOffset createdAt) =>
        new(tenantId, programId, riskId, 1,
            new RiskTreatmentActionView(actionId, riskId, "Enforce MFA", "MFA is required",
                "Identity provider policy export.", new DateOnly(2026, 10, 12), ownerId, [],
                RiskGovernanceLedger.ActionOpen, false, actor, createdAt, []));

    static RiskTreatmentActionCompletionSubmitted CompletionSubmitted(Uuid tenantId,
        Uuid programId, Uuid riskId, Uuid actionId, Uuid submissionId, Uuid submitterId,
        ActorReference actor, DateTimeOffset submittedAt, long revision) =>
        new(tenantId, programId, riskId, revision, actionId, submissionId,
            "MFA was enabled.", [Uuid.CreateVersion4()], submitterId, actor, submittedAt);

    static RiskTreatmentActionCompletionReviewed CompletionReviewed(Uuid tenantId,
        Uuid programId, Uuid riskId, Uuid actionId, string outcome, ActorReference actor,
        DateTimeOffset reviewedAt, long revision) =>
        new(tenantId, programId, riskId, revision, actionId, Uuid.CreateVersion4(), outcome,
            "Reviewed the submitted evidence.", actor, reviewedAt, null);
}
