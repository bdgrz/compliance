using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FitzControlEvaluationWorkItemDirectoryTests
{
    [Fact]
    public async Task ShouldRejectSkippedEvaluationRevisionGivenProjectedHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var evaluationId = Uuid.CreateVersion4();
        var evaluatorId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(evaluatorId, "Evaluator");
        var startedAt = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var procedures = Procedures();
        var directory = new FitzControlEvaluationWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzControlEvaluationWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "control-evaluations"));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(Started(tenantId, programId, controlId, evaluationId,
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), evaluatorId, actor, procedures, startedAt));

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyAsync(new ControlEvaluationStepRecorded(tenantId, programId,
                controlId, evaluationId, 3, ControlEvaluationLedger.StepId(evaluationId, 0),
                new EvaluationStepResultView(1, ControlEvaluationLedger.Met,
                    "Inspected the exact items.", procedures[0].InspectedItems, actor,
                    startedAt.AddMinutes(1)), null)));

        // Assert
        Assert.Contains("advance its existing source revision", failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldProjectOneReviewPerPendingRoundGivenEvaluationLifecycle()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var evaluationId = Uuid.CreateVersion4();
        var controlVersionId = Uuid.CreateVersion4();
        var planVersionId = Uuid.CreateVersion4();
        var evaluatorId = Uuid.CreateVersion4();
        var reviewerId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(evaluatorId, "Evaluator");
        var reviewer = ActorReference.ForMember(reviewerId, "Reviewer");
        var startedAt = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var procedures = Procedures();
        var directory = new FitzControlEvaluationWorkItemDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity(FitzControlEvaluationWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "control-evaluations"));
        var firstCheckpoint = new ProjectionCheckpoint(new EventCursor("evaluation-round-one"));
        var secondCheckpoint = new ProjectionCheckpoint(new EventCursor("evaluation-round-two"));
        var finalCheckpoint = new ProjectionCheckpoint(new EventCursor("evaluation-accepted"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(Started(tenantId, programId, controlId, evaluationId,
                controlVersionId, planVersionId, evaluatorId, actor, procedures, startedAt));
            await ApplyStepResultsAsync(directory, tenantId, programId, controlId, evaluationId,
                procedures, actor, 2, 1, startedAt.AddMinutes(1));
            await directory.ApplyAsync(Submitted(tenantId, programId, controlId, evaluationId,
                procedures, actor, 5, 1, startedAt.AddMinutes(2)));
            await batch.CommitAsync(firstCheckpoint);
        }
        var firstRound = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         firstCheckpoint)))
        {
            await directory.ApplyAsync(Reviewed(tenantId, programId, controlId, evaluationId,
                reviewerId, reviewer, 6, 1, "changes_requested", startedAt.AddMinutes(3)));
            await ApplyStepResultsAsync(directory, tenantId, programId, controlId, evaluationId,
                procedures, actor, 7, 2, startedAt.AddMinutes(4));
            await directory.ApplyAsync(Submitted(tenantId, programId, controlId, evaluationId,
                procedures, actor, 10, 2, startedAt.AddMinutes(5)));
            await batch.CommitAsync(secondCheckpoint);
        }
        var secondRound = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         secondCheckpoint)))
        {
            await directory.ApplyAsync(Reviewed(tenantId, programId, controlId, evaluationId,
                reviewerId, reviewer, 11, 2, ControlEvaluationLedger.Accepted,
                startedAt.AddMinutes(6)));
            await batch.CommitAsync(finalCheckpoint);
        }
        var accepted = await directory.LoadProgramAsync(tenantId, programId,
            CancellationToken.None);

        // Assert
        Assert.True(firstRound.IsSuccess);
        var first = Assert.Single(firstRound.Value);
        Assert.Equal(WorkSource.ControlEvaluationReview, first.Kind);
        Assert.Equal(evaluationId, first.SourceId);
        Assert.Equal(controlId, first.ControlId);
        Assert.Equal("Review control evaluation", first.Summary);
        Assert.Equal("Control evaluation round 1 is awaiting independent review.", first.Reason);
        Assert.Equal("review", first.NextAction);
        Assert.Equal($"/api/v1/tenants/{tenantId}/programs/{programId}/controls/{controlId}/" +
            $"evaluations/{evaluationId}/reviews", first.ActionPath);
        Assert.Equal(new OperatingHolder(OperatingAuthority.ProgramManagerHolder, programId),
            first.Responsible);
        Assert.Contains(evaluatorId, first.Excluded);
        Assert.Equal(startedAt.AddMinutes(2), first.CreatedAt);
        Assert.Equal(WorkCandidate.IdFor(Uuid.CreateVersion5(evaluationId,
            "control-evaluation-review\n1"), WorkSource.ControlEvaluationReview), first.WorkItemId);

        Assert.True(secondRound.IsSuccess);
        var second = Assert.Single(secondRound.Value);
        Assert.Equal(WorkSource.ControlEvaluationReview, second.Kind);
        Assert.NotEqual(first.WorkItemId, second.WorkItemId);
        Assert.Contains(evaluatorId, second.Excluded);
        Assert.Empty(accepted.Value);
        Assert.Equal(11, await directory.LoadRevisionAsync(tenantId));
        Assert.Equal(finalCheckpoint, await directory.LoadCheckpointAsync(tenantId));
    }

    static ControlEvaluationStarted Started(Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid evaluationId, Uuid controlVersionId, Uuid planVersionId, Uuid evaluatorId,
        ActorReference actor, IReadOnlyList<EvaluationProcedureStep> procedures,
        DateTimeOffset startedAt) => new(tenantId, programId, controlId, evaluationId, 1,
        controlVersionId, procedures, evaluatorId, actor, startedAt, null, [])
        {
            PlanVersionId = planVersionId,
            PlanVersion = 1,
        };

    static async Task ApplyStepResultsAsync(FitzControlEvaluationWorkItemDirectory directory,
        Uuid tenantId, Uuid programId, Uuid controlId, Uuid evaluationId,
        IReadOnlyList<EvaluationProcedureStep> procedures, ActorReference actor, long firstRevision,
        int round, DateTimeOffset recordedAt)
    {
        for (var index = 0; index < procedures.Count; index++)
        {
            var procedure = procedures[index];
            await directory.ApplyAsync(new ControlEvaluationStepRecorded(tenantId, programId,
                controlId, evaluationId, firstRevision + index,
                ControlEvaluationLedger.StepId(evaluationId, index),
                new EvaluationStepResultView(round, ControlEvaluationLedger.Met,
                    "Inspected the exact items.", procedure.InspectedItems, actor,
                    recordedAt.AddMinutes(index)), null));
        }
    }

    static ControlEvaluationSubmitted Submitted(Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid evaluationId, IReadOnlyList<EvaluationProcedureStep> procedures, ActorReference actor,
        long revision, int round, DateTimeOffset submittedAt)
    {
        var steps = procedures.Select((procedure, index) => new ControlEvaluationStepView(
            ControlEvaluationLedger.StepId(evaluationId, index), index, procedure.Assertion,
            procedure.Method, procedure.InspectedItems, procedure.ExpectedCondition,
            new EvaluationStepResultView(round, ControlEvaluationLedger.Met,
                "Inspected the exact items.", procedure.InspectedItems, actor,
                submittedAt.AddMinutes(-1)))).ToArray();
        return new ControlEvaluationSubmitted(tenantId, programId, controlId, evaluationId,
            revision, new EvaluationSubmissionView(round,
                [new("design", ControlEvaluationLedger.Effective, "Design is effective."),
                    new("implementation", ControlEvaluationLedger.Effective,
                        "Implementation is effective."),
                    new("evidence_sufficiency", ControlEvaluationLedger.Effective,
                        "Evidence is sufficient.")], ControlEvaluationLedger.Effective, steps, [], actor,
                submittedAt));
    }

    static ControlEvaluationReviewed Reviewed(Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid evaluationId, Uuid reviewerId, ActorReference reviewer, long revision, int round,
        string decision, DateTimeOffset reviewedAt) => new(tenantId, programId, controlId,
        evaluationId, revision, new ControlEvaluationReviewView(Uuid.CreateVersion4(), round,
            decision, "Reviewed the submitted evaluation.", reviewerId, reviewer, reviewedAt));

    static IReadOnlyList<EvaluationProcedureStep> Procedures() =>
    [
        new("design", "inspection", [new("record", "policy/access-review", "v1")],
            "The policy requires an access review."),
        new("implementation", "reperformance", [new("artifact", "exports/q3.csv", "sha256:ab12")],
            "The export contains every active account."),
        new("evidence_sufficiency", "inspection", [new("artifact", "exports/q3.csv", "sha256:ab12")],
            "The export is complete and dated."),
    ];
}
