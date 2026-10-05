using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Tests.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Evaluations;

public sealed class ControlEvaluationPlanHandlerTests
{
    static readonly IReadOnlyList<EvaluationProcedureStep> Steps =
    [
        new("design", "inspection",
            [new EvaluationInspectedItem("record", "policy/access-review", "v3")],
            "Policy requires a monthly signed review."),
        new("implementation", "reperformance",
            [new EvaluationInspectedItem("artifact", "exports/q3.csv", "sha256:ab12")],
            "Every leaver was removed."),
    ];

    [Fact]
    public async Task ShouldReadStablePlanHistoryGivenManagerDefinesSuccessor()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var first = await fixture.DefineEvaluationPlanAsync(0,
            "Management reviews access.", Steps, testerIndependenceRequired: true);

        // Act
        var second = await fixture.DefineEvaluationPlanAsync(first.Version,
            "Management reviews privileged access monthly.", Steps);
        var read = await fixture.AsAsync(fixture.OutsiderUserId,
            new GetControlEvaluationPlanVersion(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, first.PlanVersionId));
        var history = await fixture.AsAsync(fixture.OutsiderUserId,
            new ListControlEvaluationPlanVersions(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId));

        // Assert
        Assert.Equal(1, first.Version);
        Assert.Equal(2, second.Version);
        Assert.Equal(fixture.ControlVersionId, first.ControlVersionId);
        Assert.Equal(first.Objective, read.Objective);
        Assert.True(read.TesterIndependenceRequired);
        Assert.Equal([1L, 2L], history.Items.Select(static item => item.Version));
        Assert.Equal(first.PlanVersionId, history.Items[0].PlanVersionId);
    }

    [Fact]
    public async Task ShouldRejectUnsupportedPageLimitGivenPlanHistoryList()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();

        // Act
        // Assert
        await fixture.Scenario(fixture.OutsiderUserId)
            .When(new ListControlEvaluationPlanVersions(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, 0))
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.OutsiderUserId)
            .When(new ListControlEvaluationPlanVersions(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, 201))
            .ExpectFailure(RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldRequireProgramManagerGivenPlanDefinition()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();

        // Act
        // Assert
        await fixture.Scenario(fixture.OwnerUserId)
            .When(new DefineControlEvaluationPlan(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, fixture.ControlVersionId, 0, "Evaluate the control.",
                Steps, false))
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldRequireApprovedControlVersionGivenPlanDefinition()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();

        // Act
        // Assert
        await fixture.Scenario(fixture.LeadUserId)
            .When(new DefineControlEvaluationPlan(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, Uuid.CreateVersion4(), 0, "Evaluate the control.", Steps, false))
            .ExpectFailure(RequestErrorKind.NotFound);
    }

    [Fact]
    public async Task ShouldFreezeExactPlanAndRejectOldVersionGivenPlanChangesAfterStart()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var first = await fixture.DefineEvaluationPlanAsync(0, "Evaluate access review.", Steps);

        // Act
        var started = await fixture.AsAsync(fixture.OwnerUserId, new StartControlEvaluation(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, first.PlanVersionId));
        var second = await fixture.DefineEvaluationPlanAsync(first.Version,
            "Evaluate privileged access review.", Steps);
        var read = await fixture.AsAsync(fixture.OutsiderUserId, new GetControlEvaluation(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, started.EvaluationId));

        // Assert
        Assert.Equal(first.PlanVersionId, read.PlanVersionId);
        Assert.Equal(first.Version, read.PlanVersion);
        Assert.Equal(first.Steps.Count, read.Steps.Count);
        for (var index = 0; index < first.Steps.Count; index++)
        {
            Assert.Equal(first.Steps[index].Assertion, read.Steps[index].Assertion);
            Assert.Equal(first.Steps[index].Method, read.Steps[index].Method);
            Assert.Equal(first.Steps[index].ExpectedCondition,
                read.Steps[index].ExpectedCondition);
            Assert.Equal(first.Steps[index].InspectedItems.Select(static item =>
                    (item.Kind, item.Reference, item.Version)),
                read.Steps[index].InspectedItems.Select(static item =>
                    (item.Kind, item.Reference, item.Version)));
        }
        Assert.Equal(2, second.Version);
        await fixture.Scenario(fixture.OwnerUserId)
            .When(new StartControlEvaluation(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, first.PlanVersionId))
            .ExpectFailure(RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldRequireIndependentEvaluatorGivenPlanFlag()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.DefineEvaluationPlanAsync(0, "Independent test.", Steps,
            testerIndependenceRequired: true);

        // Act
        await fixture.Scenario(fixture.OwnerUserId)
            .When(new StartControlEvaluation(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, plan.PlanVersionId))
            .ExpectFailure(RequestErrorKind.Forbidden);
        var startedByManager = await fixture.AsAsync(fixture.LeadUserId,
            new StartControlEvaluation(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, plan.PlanVersionId));

        // Assert
        Assert.Equal(fixture.LeadMemberId, startedByManager.EvaluatorMemberId);
    }

    [Fact]
    public async Task ShouldNotDisclosePlanGivenDifferentTenantOrControl()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.GetOrDefineEvaluationPlanAsync();

        // Act
        // Assert
        await fixture.Scenario(fixture.OutsiderUserId)
            .When(new GetControlEvaluationPlanVersion(Uuid.CreateVersion4(), fixture.ProgramId,
                fixture.ControlId, plan.PlanVersionId))
            .ExpectFailure(RequestErrorKind.NotFound);
        await fixture.Scenario(fixture.OutsiderUserId)
            .When(new ListControlEvaluationPlanVersions(fixture.TenantId, fixture.ProgramId,
                Uuid.CreateVersion4()))
            .ExpectFailure(RequestErrorKind.NotFound);
    }
}
