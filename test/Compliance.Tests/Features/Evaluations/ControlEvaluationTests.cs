using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Tests.Features.Operations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Evaluations;

public sealed class ControlEvaluationTests
{
    static readonly EvaluationInspectedItem Policy = new("policy", "policy/access-review", "v3");
    static readonly EvaluationInspectedItem Export = new("artifact", "exports/q3.csv", "sha256:ab12");

    static IReadOnlyList<EvaluationProcedureStep> Procedure =>
    [
        new("design", "inspection", [Policy], "Policy requires a monthly signed review."),
        new("implementation", "reperformance", [Export], "Every leaver was removed."),
        new("evidence_sufficiency", "inspection", [Export], "Export is complete and dated."),
    ];

    static IReadOnlyList<EvaluationAssertionConclusion> AllEffective =>
    [
        new("design", "effective", "Design meets the objective."),
        new("implementation", "effective", "Operates as designed."),
        new("evidence_sufficiency", "effective", "Evidence is sufficient."),
    ];

    internal static async Task<ControlEvaluationView> StartAsync(OperationsFixture fixture,
        Uuid? userId = null, Uuid? retestOf = null, Uuid? planVersionId = null)
    {
        var plan = await fixture.GetOrDefineEvaluationPlanAsync();
        return await fixture.AsAsync(userId ?? fixture.OwnerUserId, new StartControlEvaluation(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId,
            planVersionId ?? plan.PlanVersionId, retestOf));
    }

    static RecordControlEvaluationStep Record(OperationsFixture fixture,
        ControlEvaluationView evaluation, int index, string result = "met",
        string? classification = null) => new(fixture.TenantId, fixture.ProgramId,
        fixture.ControlId, evaluation.EvaluationId, evaluation.Steps[index].StepId,
        evaluation.Revision, result, "Inspected the exact items.",
        evaluation.Steps[index].InspectedItems, classification,
        classification is null ? null : "Two leavers kept access.");

    internal static async Task<ControlEvaluationView> RecordAllAsync(OperationsFixture fixture,
        ControlEvaluationView evaluation, Uuid? userId = null, string implementation = "met",
        string? classification = null)
    {
        var user = userId ?? fixture.OwnerUserId;
        for (var index = 0; index < evaluation.Steps.Count; index++)
            evaluation = await fixture.AsAsync(user, Record(fixture, evaluation, index,
                index == 1 ? implementation : "met", index == 1 ? classification : null));
        return evaluation;
    }

    internal static SubmitControlEvaluation Submit(OperationsFixture fixture,
        ControlEvaluationView evaluation,
        IReadOnlyList<EvaluationAssertionConclusion>? conclusions = null) => new(fixture.TenantId,
        fixture.ProgramId, fixture.ControlId, evaluation.EvaluationId, evaluation.Revision,
        conclusions ?? AllEffective);

    internal static ReviewControlEvaluation Review(OperationsFixture fixture,
        ControlEvaluationView evaluation, string decision, Uuid? waiverId = null) => new(
        fixture.TenantId, fixture.ProgramId, fixture.ControlId, evaluation.EvaluationId,
        evaluation.Revision, decision, "Reviewed independently.", waiverId);

    internal static async Task<ControlEvaluationView> SubmittedAsync(OperationsFixture fixture)
    {
        var evaluation = await RecordAllAsync(fixture, await StartAsync(fixture));
        return await fixture.AsAsync(fixture.OwnerUserId, Submit(fixture, evaluation));
    }

    [Fact]
    public async Task ShouldFreezeProcedureAndExactControlVersionGivenOwnerStartsEvaluation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();

        // Act
        var evaluation = await StartAsync(fixture);

        // Assert
        Assert.Equal("in_progress", evaluation.State);
        Assert.Equal(fixture.ControlVersionId, evaluation.ControlVersionId);
        Assert.Equal(fixture.OwnerMemberId, evaluation.EvaluatorMemberId);
        Assert.Equal(["design", "implementation", "evidence_sufficiency"],
            evaluation.Steps.Select(static step => step.Assertion));
        Assert.Equal(Policy, Assert.Single(evaluation.Steps[0].InspectedItems));
        Assert.Equal("not_required", evaluation.RetestStatus);
        var read = await fixture.AsAsync(fixture.OutsiderUserId, new GetControlEvaluation(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, evaluation.EvaluationId));
        Assert.Equal(evaluation.Steps.Select(static step => step.StepId),
            read.Steps.Select(static step => step.StepId));
    }

    [Fact]
    public async Task ShouldRejectStartGivenMemberWhoNeitherOwnsNorManages()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.GetOrDefineEvaluationPlanAsync(Procedure);

        await fixture.Scenario(fixture.OutsiderUserId)
            // Act
            .When(new StartControlEvaluation(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, plan.PlanVersionId))
            // Assert
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldRejectPlanGivenOperatingEffectivenessAssertionOrMissingVersion()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();

        await fixture.Scenario(fixture.LeadUserId)
            // Act
            .When(new DefineControlEvaluationPlan(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, fixture.ControlVersionId, 0, "Evaluate the control.",
                [new("operating_effectiveness", "inspection", [Policy], "x")], false))
            // Assert
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.LeadUserId)
            .When(new DefineControlEvaluationPlan(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, fixture.ControlVersionId, 0, "Evaluate the control.",
                [new("design", "inspection", [new("record", "p", " ")], "x")], false))
            .ExpectFailure(RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldRequireDeviationClassificationGivenNotMetStep()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await StartAsync(fixture);

        await fixture.Scenario(fixture.OwnerUserId)
            // Act
            .When(Record(fixture, evaluation, 1, "not_met"))
            // Assert
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.LeadUserId)
            .When(Record(fixture, evaluation, 0))
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldDeriveEffectiveOverallGivenEveryStepMet()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await RecordAllAsync(fixture, await StartAsync(fixture));

        // Act
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, Submit(fixture, evaluation));

        // Assert
        Assert.Equal("submitted", submitted.State);
        var submission = Assert.Single(submitted.Submissions);
        Assert.Equal("effective", submission.Overall);
        Assert.Equal(3, submission.Conclusions.Count);
        Assert.All(submission.Steps, step => Assert.Equal("met", step.Result!.Result));
    }

    [Fact]
    public async Task ShouldRejectConflatedConclusionsGivenMissingAssertionOrUnsupportedEffective()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await RecordAllAsync(fixture, await StartAsync(fixture),
            implementation: "not_met", classification: "material");

        // Act
        // Assert
        await PersonalControlEvaluationTransportTests.HttpAsync(fixture.Provider, fixture.OwnerUserId, Submit(fixture, evaluation), RequestErrorKind.Validation);
        await PersonalControlEvaluationTransportTests.HttpAsync(fixture.Provider, fixture.OwnerUserId, Submit(fixture, evaluation, [AllEffective[0], AllEffective[2]]), RequestErrorKind.Validation);
        await PersonalControlEvaluationTransportTests.HttpAsync(fixture.Provider, fixture.OwnerUserId, Submit(fixture, evaluation, [.. AllEffective,
                new("operating_effectiveness", "effective", "Type II.")]), RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldRequireDispositionGivenMinorDeviation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await RecordAllAsync(fixture, await StartAsync(fixture),
            implementation: "not_met", classification: "minor");
        IReadOnlyList<EvaluationAssertionConclusion> conclusions =
        [
            AllEffective[0],
            new("implementation", "effective_with_exceptions", "One minor exception."),
            AllEffective[2],
        ];
        await PersonalControlEvaluationTransportTests.HttpAsync(fixture.Provider, fixture.OwnerUserId, Submit(fixture, evaluation, conclusions), RequestErrorKind.Conflict);
        var deviation = Assert.Single(evaluation.Deviations);
        Assert.Equal("pending_disposition", deviation.Status);

        // Act
        var disposed = await fixture.AsAsync(fixture.OwnerUserId,
            new DisposeControlEvaluationDeviation(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, evaluation.EvaluationId, deviation.DeviationId,
                evaluation.Revision, "corrected", "Access removed the same day."));
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, Submit(fixture, disposed,
            conclusions));

        // Assert
        Assert.Equal("dispositioned", Assert.Single(submitted.Deviations).Status);
        Assert.Equal("effective_with_exceptions", submitted.Submissions[0].Overall);
    }

    [Fact]
    public async Task ShouldBlockSelfReviewGivenEvaluatorWithoutWaiver()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await RecordAllAsync(fixture, await StartAsync(fixture, fixture.LeadUserId),
            fixture.LeadUserId);
        var submitted = await fixture.AsAsync(fixture.LeadUserId, Submit(fixture, evaluation));

        // Act
        // Assert
        await PersonalControlEvaluationTransportTests.HttpAsync(fixture.Provider, fixture.LeadUserId, Review(fixture, submitted, "accepted"), RequestErrorKind.Forbidden);
        await PersonalControlEvaluationTransportTests.HttpAsync(fixture.Provider, fixture.OwnerUserId, Review(fixture, submitted, "accepted"), RequestErrorKind.Forbidden);
        var waiverId = await fixture.ApprovedWaiverAsync(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.ControlEvaluation, submitted.EvaluationId,
            submitted.EvaluationId, 1, SeparationOfDutiesActions.Review), fixture.LeadMemberId);
        var accepted = await fixture.AsAsync(fixture.LeadUserId, Review(fixture, submitted,
            "accepted", waiverId));
        Assert.Equal("accepted", accepted.State);
        Assert.Equal(waiverId, accepted.LatestReview!.SeparationOfDutiesWaiverId);
    }

    [Fact]
    public async Task ShouldReopenWorkAndKeepSubmissionGivenRejection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var submitted = await SubmittedAsync(fixture);

        // Act
        var rejected = await fixture.AsAsync(fixture.ApproverUserId,
            Review(fixture, submitted, "rejected"));

        // Assert
        Assert.Equal("in_progress", rejected.State);
        Assert.Equal(2, rejected.Round);
        Assert.All(rejected.Steps, step => Assert.Null(step.Result));
        var kept = Assert.Single(rejected.Submissions);
        Assert.All(kept.Steps, step => Assert.Equal("met", step.Result!.Result));
        Assert.Equal("rejected", rejected.LatestReview!.Decision);
        var resubmitted = await fixture.AsAsync(fixture.OwnerUserId,
            Submit(fixture, await RecordAllAsync(fixture, rejected)));
        var accepted = await fixture.AsAsync(fixture.ApproverUserId,
            Review(fixture, resubmitted, "accepted"));
        Assert.Equal(["rejected", "accepted"], accepted.Reviews.Select(static r => r.Decision));
        Assert.Equal("effective", accepted.AcceptedOverall);
    }

    [Fact]
    public async Task ShouldKeepResultsForRevisionGivenChangesRequested()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var submitted = await SubmittedAsync(fixture);

        // Act
        var returned = await fixture.AsAsync(fixture.ApproverUserId,
            Review(fixture, submitted, "changes_requested"));

        // Assert
        Assert.Equal("in_progress", returned.State);
        Assert.All(returned.Steps, step => Assert.NotNull(step.Result));
        var resubmitted = await fixture.AsAsync(fixture.OwnerUserId, Submit(fixture, returned));
        Assert.Equal(2, resubmitted.Submissions.Count);
        await PersonalControlEvaluationTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId, Review(fixture, submitted, "accepted"), RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldRequireRetestAndPreserveOriginalGivenAcceptedMaterialDeviation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await RecordAllAsync(fixture, await StartAsync(fixture),
            implementation: "not_met", classification: "material");
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, Submit(fixture, evaluation,
        [
            AllEffective[0], new("implementation", "ineffective", "Leavers kept access."),
            AllEffective[2],
        ]));
        var accepted = await fixture.AsAsync(fixture.ApproverUserId,
            Review(fixture, submitted, "accepted"));
        var deviation = Assert.Single(accepted.Deviations);
        Assert.Equal("ineffective", accepted.AcceptedOverall);
        Assert.Equal("required", accepted.RetestStatus);
        Assert.Equal(Uuid.CreateVersion5(deviation.DeviationId, "finding"), deviation.FindingId);

        // Act
        var retest = await StartAsync(fixture, retestOf: accepted.EvaluationId);
        var passed = await fixture.AsAsync(fixture.ApproverUserId, Review(fixture,
            await fixture.AsAsync(fixture.OwnerUserId, Submit(fixture,
                await RecordAllAsync(fixture, retest))), "accepted"));

        // Assert
        Assert.Equal(accepted.EvaluationId, passed.RetestOfEvaluationId);
        Assert.Equal(accepted.PlanVersionId, retest.PlanVersionId);
        Assert.Equal(accepted.PlanVersion, retest.PlanVersion);
        Assert.Equal([deviation.DeviationId], passed.RetestOfDeviationIds);
        Assert.Equal(accepted.Steps.Select(static s => s.ExpectedCondition),
            retest.Steps.Select(static s => s.ExpectedCondition));
        var original = await fixture.AsAsync(fixture.OutsiderUserId, new GetControlEvaluation(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, accepted.EvaluationId));
        Assert.Equal("passed", original.RetestStatus);
        Assert.Equal([retest.EvaluationId], original.RetestEvaluationIds);
        Assert.Equal("ineffective", original.AcceptedOverall);
        Assert.Equal(deviation, Assert.Single(original.Deviations));
        var kept = Assert.Single(original.Submissions);
        Assert.Equal(deviation.DeviationId, Assert.Single(kept.Deviations).DeviationId);
        Assert.Equal("not_met", kept.Steps[1].Result!.Result);
        Assert.Equal("accepted", Assert.Single(original.Reviews).Decision);
    }

    [Fact]
    public async Task ShouldAllowSuccessorPlanGivenMaterialDeviationRetest()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var firstPlan = await fixture.GetOrDefineEvaluationPlanAsync(Procedure);
        var evaluation = await StartAsync(fixture, planVersionId: firstPlan.PlanVersionId);
        var withDeviation = await RecordAllAsync(fixture, evaluation,
            implementation: "not_met", classification: "material");
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, Submit(fixture, withDeviation,
        [
            AllEffective[0], new("implementation", "ineffective", "Leavers kept access."),
            AllEffective[2],
        ]));
        var accepted = await fixture.AsAsync(fixture.ApproverUserId,
            Review(fixture, submitted, "accepted"));
        var successor = await fixture.DefineEvaluationPlanAsync(firstPlan.Version,
            "Recheck access after remediation.", Procedure);

        // Act
        var retest = await StartAsync(fixture, retestOf: accepted.EvaluationId,
            planVersionId: successor.PlanVersionId);

        // Assert
        Assert.Equal(successor.PlanVersionId, retest.PlanVersionId);
        Assert.Equal(successor.Version, retest.PlanVersion);
        Assert.Equal(accepted.EvaluationId, retest.RetestOfEvaluationId);
    }

    [Fact]
    public async Task ShouldRejectRetestGivenEvaluationWithoutMaterialDeviation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var accepted = await fixture.AsAsync(fixture.ApproverUserId,
            Review(fixture, await SubmittedAsync(fixture), "accepted"));
        var plan = await fixture.GetOrDefineEvaluationPlanAsync();

        await fixture.Scenario(fixture.OwnerUserId)
            // Act
            .When(new StartControlEvaluation(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, plan.PlanVersionId, accepted.EvaluationId))
            // Assert
            .ExpectFailure(RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldNotDiscloseGivenEvaluationFromAnotherTenantOrControl()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await StartAsync(fixture);

        await fixture.Scenario(fixture.LeadUserId)
            // Act
            .When(new GetControlEvaluation(Uuid.CreateVersion4(), fixture.ProgramId,
                fixture.ControlId, evaluation.EvaluationId))
            // Assert
            .ExpectFailure(RequestErrorKind.NotFound);
        await fixture.Scenario(fixture.LeadUserId)
            .When(new GetControlEvaluation(fixture.TenantId, fixture.ProgramId,
                Uuid.CreateVersion4(), evaluation.EvaluationId))
            .ExpectFailure(RequestErrorKind.NotFound);
        var list = await fixture.AsAsync(fixture.OutsiderUserId, new ListControlEvaluations(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, "in_progress"));
        Assert.Single(list.Items);
    }

    [Fact]
    public async Task ShouldRejectStaleRevisionGivenConcurrentStepRecord()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await StartAsync(fixture);
        await fixture.AsAsync(fixture.OwnerUserId, Record(fixture, evaluation, 0));

        await fixture.Scenario(fixture.OwnerUserId)
            // Act
            .When(Record(fixture, evaluation, 1))
            // Assert
            .ExpectFailure(RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldRejectWaiverDispositionGivenWaiverScopedToAnotherRecord()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await RecordAllAsync(fixture, await StartAsync(fixture),
            implementation: "not_met", classification: "minor");
        var deviation = Assert.Single(evaluation.Deviations);
        var unrelated = await fixture.ApprovedWaiverAsync(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.Finding, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1,
            SeparationOfDutiesActions.Approve), fixture.OwnerMemberId);
        var scoped = await fixture.ApprovedWaiverAsync(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.ControlEvaluation, evaluation.EvaluationId,
            deviation.DeviationId, 1, SeparationOfDutiesActions.Approve), fixture.OwnerMemberId);
        DisposeControlEvaluationDeviation Dispose(Uuid waiverId) => new(fixture.TenantId,
            fixture.ProgramId, fixture.ControlId, evaluation.EvaluationId, deviation.DeviationId,
            evaluation.Revision, "accepted_with_waiver", "Accepted for this period.", waiverId);

        await fixture.Scenario(fixture.OwnerUserId)
            // Act
            .When(Dispose(unrelated))
            // Assert
            .ExpectFailure(RequestErrorKind.Conflict);
        var disposed = await fixture.AsAsync(fixture.OwnerUserId, Dispose(scoped));
        Assert.Equal(scoped, Assert.Single(disposed.Deviations).WaiverId);
    }

    [Fact]
    public async Task ShouldReuseDeviationIdentityGivenResubmissionAfterRejection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        IReadOnlyList<EvaluationAssertionConclusion> conclusions =
        [
            AllEffective[0], new("implementation", "ineffective", "Leavers kept access."),
            AllEffective[2],
        ];
        var first = await fixture.AsAsync(fixture.OwnerUserId, Submit(fixture,
            await RecordAllAsync(fixture, await StartAsync(fixture), implementation: "not_met",
                classification: "material"), conclusions));
        var rejected = await fixture.AsAsync(fixture.ApproverUserId,
            Review(fixture, first, "rejected"));

        // Act
        var second = await fixture.AsAsync(fixture.OwnerUserId, Submit(fixture,
            await RecordAllAsync(fixture, rejected, implementation: "not_met",
                classification: "material"), conclusions));

        // Assert
        var findingIds = second.Submissions.SelectMany(static s => s.Deviations)
            .Select(static d => d.FindingId).Distinct().ToArray();
        Assert.Equal(2, second.Submissions.Count);
        Assert.Single(findingIds);
        Assert.Single(second.Submissions.SelectMany(static s => s.Deviations)
            .Select(static d => d.DeviationId).Distinct());
    }

    [Fact]
    public async Task ShouldReportPassedGivenLaterAbandonedRetest()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, Submit(fixture,
            await RecordAllAsync(fixture, await StartAsync(fixture), implementation: "not_met",
                classification: "material"),
        [
            AllEffective[0], new("implementation", "ineffective", "Leavers kept access."),
            AllEffective[2],
        ]));
        var accepted = await fixture.AsAsync(fixture.ApproverUserId,
            Review(fixture, submitted, "accepted"));
        var retest = await StartAsync(fixture, retestOf: accepted.EvaluationId);
        await fixture.AsAsync(fixture.ApproverUserId, Review(fixture,
            await fixture.AsAsync(fixture.OwnerUserId, Submit(fixture,
                await RecordAllAsync(fixture, retest))), "accepted"));

        // Act
        await StartAsync(fixture, retestOf: accepted.EvaluationId);

        // Assert
        var original = await fixture.AsAsync(fixture.OutsiderUserId, new GetControlEvaluation(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, accepted.EvaluationId));
        Assert.Equal("passed", original.RetestStatus);
        Assert.Equal(2, original.RetestEvaluationIds.Count);
    }

    [Fact]
    public async Task ShouldNotDiscloseGivenControlNoLongerVisibleInProgram()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await StartAsync(fixture);

        await fixture.Scenario(fixture.LeadUserId)
            // Act
            .When(new GetControlEvaluation(fixture.TenantId, Uuid.CreateVersion4(),
                fixture.ControlId, evaluation.EvaluationId))
            // Assert
            .ExpectFailure(RequestErrorKind.NotFound);
    }
}
