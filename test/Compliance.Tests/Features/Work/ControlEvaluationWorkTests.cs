using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class ControlEvaluationWorkTests
{
    static readonly EvaluationInspectedItem Policy = new("record", "policy/access-review", "v3");
    static readonly EvaluationInspectedItem Export = new("artifact", "exports/q3.csv", "sha256:ab12");

    [Fact]
    public async Task ShouldShowSubmittedControlEvaluationGivenIndependentReviewPending()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await SubmitAsync(fixture);

        // Act
        var queue = await fixture.AsAsync(fixture.LeadUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        var item = Assert.Single(queue.Items);
        Assert.Equal("control_evaluation_review", item.Kind);
        Assert.Equal(evaluation.EvaluationId, item.SourceId);
        Assert.Equal(fixture.ControlId, item.ControlId);
        Assert.Equal("review", item.NextAction);
        Assert.Null(item.AssigneeMemberId);
        Assert.Equal(new OperatingHolder(OperatingAuthority.ProgramManagerHolder,
            fixture.ProgramId), item.Responsible);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/controls/" +
                     $"{fixture.ControlId}/evaluations/{evaluation.EvaluationId}/reviews",
            item.ActionPath);
    }

    [Theory]
    [InlineData("accepted")]
    [InlineData("rejected")]
    [InlineData("changes_requested")]
    public async Task ShouldRemoveControlEvaluationWorkGivenReviewDecision(string decision)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await SubmitAsync(fixture);

        // Act
        await fixture.AsAsync(fixture.LeadUserId, new ReviewControlEvaluation(fixture.TenantId,
            fixture.ProgramId, fixture.ControlId, evaluation.EvaluationId, evaluation.Revision,
            decision, "Reviewed independently."));
        var queue = await fixture.AsAsync(fixture.LeadUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        Assert.DoesNotContain(queue.Items,
            static item => item.Kind == "control_evaluation_review");
    }

    [Fact]
    public async Task ShouldCreateDistinctWorkGivenEvaluationResubmittedAfterChangesRequested()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await SubmitAsync(fixture);
        var firstQueue = await fixture.AsAsync(fixture.LeadUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var firstWork = Assert.Single(firstQueue.Items);
        var returned = await fixture.AsAsync(fixture.LeadUserId,
            new ReviewControlEvaluation(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, evaluation.EvaluationId, evaluation.Revision,
                "changes_requested", "Clarify the inspected population."));

        // Act
        var resubmitted = await SubmitRoundAsync(fixture, returned);
        var secondQueue = await fixture.AsAsync(fixture.LeadUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        var secondWork = Assert.Single(secondQueue.Items);
        Assert.Equal(firstWork.Kind, secondWork.Kind);
        Assert.Equal(firstWork.SourceId, secondWork.SourceId);
        Assert.Equal(firstWork.ControlId, secondWork.ControlId);
        Assert.NotEqual(firstWork.WorkItemId, secondWork.WorkItemId);
        Assert.Equal(2, resubmitted.Round);
    }

    [Fact]
    public async Task ShouldHideControlEvaluationReviewFromEvaluatorGivenSeparationOfDuties()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await SubmitAsync(fixture);

        // Act
        var evaluatorQueue = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "all"));
        var reviewerQueue = await fixture.AsAsync(fixture.LeadUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        Assert.DoesNotContain(evaluatorQueue.Items,
            item => item.SourceId == evaluation.EvaluationId);
        Assert.Contains(reviewerQueue.Items,
            item => item.SourceId == evaluation.EvaluationId && item.Kind == "control_evaluation_review");
    }

    [Fact]
    public async Task ShouldKeepControlEvaluationWorkWithinItsTenantAndProgramGivenDifferentTenantOrProgram()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await SubmitAsync(fixture);

        // Act
        var otherProgram = await fixture.AsAsync(fixture.LeadUserId,
            new ListWork(fixture.TenantId, Uuid.CreateVersion4(), "all"));
        var otherTenant = await fixture.AsAsync(fixture.LeadUserId,
            new ListWork(Uuid.CreateVersion4(), fixture.ProgramId, "all"));

        // Assert
        Assert.DoesNotContain(otherProgram.Items,
            item => item.SourceId == evaluation.EvaluationId);
        Assert.DoesNotContain(otherTenant.Items,
            item => item.SourceId == evaluation.EvaluationId);
    }

    static async Task<ControlEvaluationView> SubmitAsync(OperationsFixture fixture)
    {
        var plan = await fixture.GetOrDefineEvaluationPlanAsync(Procedure);
        var evaluation = await fixture.AsAsync(fixture.OwnerUserId,
            new StartControlEvaluation(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                plan.PlanVersionId));
        return await SubmitRoundAsync(fixture, evaluation);
    }

    static async Task<ControlEvaluationView> SubmitRoundAsync(OperationsFixture fixture,
        ControlEvaluationView evaluation)
    {
        for (var index = 0; index < evaluation.Steps.Count; index++)
            evaluation = await fixture.AsAsync(fixture.OwnerUserId,
                new RecordControlEvaluationStep(fixture.TenantId, fixture.ProgramId,
                    fixture.ControlId, evaluation.EvaluationId, evaluation.Steps[index].StepId,
                    evaluation.Revision, "met", "Inspected the exact items.",
                    evaluation.Steps[index].InspectedItems));
        return await fixture.AsAsync(fixture.OwnerUserId,
            new SubmitControlEvaluation(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                evaluation.EvaluationId, evaluation.Revision,
                [new("design", "effective", "Design meets the objective."),
                    new("implementation", "effective", "Operates as designed."),
                    new("evidence_sufficiency", "effective", "Evidence is sufficient.")]));
    }

    static IReadOnlyList<EvaluationProcedureStep> Procedure =>
    [
        new("design", "inspection", [Policy], "Policy requires a monthly signed review."),
        new("implementation", "reperformance", [Export], "Every leaver was removed."),
        new("evidence_sufficiency", "inspection", [Export], "Export is complete and dated."),
    ];
}
