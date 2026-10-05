using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Evaluations;

public sealed class ControlEvaluationPlanTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = Uuid.CreateVersion4();
    static readonly Uuid ControlVersionId = Uuid.CreateVersion4();
    static readonly Uuid AuthorMemberId = Uuid.CreateVersion4();
    static readonly ActorReference Author = ActorReference.ForMember(AuthorMemberId, "Lead");
    static readonly DateTimeOffset ChangedAt = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly IReadOnlyList<EvaluationProcedureStep> Steps =
    [
        new("design", "inquiry",
            [new EvaluationInspectedItem("boundary", "boundary/abc", "version/1")],
            "Management defined the review cadence."),
        new("implementation", "inspection",
            [new EvaluationInspectedItem("artifact", "evidence/xyz", "sha256:ab12")],
            "A signed review record is retained."),
    ];

    [Fact]
    public void ShouldRetainImmutablePlanVersionsGivenPlanRevision()
    {
        // Arrange
        var plan = new ControlEvaluationPlan(TenantId, ProgramId, ControlId);
        var firstVersionId = Uuid.CreateVersion4();

        // Act
        var firstFailure = plan.Define(0, ControlVersionId, firstVersionId,
            "Management reviews access.", Steps,
            true, AuthorMemberId, Author, ChangedAt);
        var first = Assert.Single(plan.ReadVersions());
        var secondVersionId = Uuid.CreateVersion4();
        var secondFailure = plan.Define(1, ControlVersionId, secondVersionId,
            "Management reviews privileged access monthly.", Steps, false, AuthorMemberId,
            Author, ChangedAt.AddDays(1));
        var stale = plan.Define(1, ControlVersionId, Uuid.CreateVersion4(), "Stale revision.", Steps, true,
            AuthorMemberId, Author, ChangedAt.AddDays(2));

        // Assert
        Assert.Null(firstFailure);
        Assert.Null(secondFailure);
        Assert.Equal(2, plan.Revision);
        Assert.Equal(2, plan.ReadVersions().Count);
        Assert.Equal(firstVersionId, first.PlanVersionId);
        Assert.Equal(ControlVersionId, first.ControlVersionId);
        Assert.Equal("Management reviews access.", first.Objective);
        Assert.True(first.TesterIndependenceRequired);
        Assert.Equal(Steps.Select(static step => step.Assertion),
            first.Steps.Select(static step => step.Assertion));
        Assert.Equal(Steps.Select(static step => step.Method),
            first.Steps.Select(static step => step.Method));
        Assert.Equal(Steps.Select(static step => step.ExpectedCondition),
            first.Steps.Select(static step => step.ExpectedCondition));
        Assert.Equal(Steps.SelectMany(static step => step.InspectedItems)
                .Select(static item => (item.Kind, item.Reference, item.Version)),
            first.Steps.SelectMany(static step => step.InspectedItems)
                .Select(static item => (item.Kind, item.Reference, item.Version)));
        Assert.Equal(secondVersionId, plan.CurrentVersion!.PlanVersionId);
        Assert.Equal("Management reviews privileged access monthly.",
            plan.CurrentVersion.Objective);
        Assert.NotNull(stale);
        Assert.Equal(CommandFailureCode.VersionConflict, stale!.Code);
    }

    [Fact]
    public void ShouldRejectInvalidProcedureGivenPlanDefinition()
    {
        // Arrange
        var plan = new ControlEvaluationPlan(TenantId, ProgramId, ControlId);

        // Act
        var missingObjective = plan.Define(0, ControlVersionId, Uuid.CreateVersion4(), " ", Steps, true,
            AuthorMemberId, Author, ChangedAt);
        var missingInspection = plan.Define(0, ControlVersionId, Uuid.CreateVersion4(),
            "Management reviews access.",
            [new("evidence_sufficiency", "inspection", [], "Evidence is sufficient.")],
            true, AuthorMemberId, Author, ChangedAt);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, missingObjective!.Code);
        Assert.Equal(CommandFailureCode.InvalidContent, missingInspection!.Code);
        Assert.Empty(plan.ReadVersions());
    }

    [Fact]
    public void ShouldAcceptIdempotentRetryGivenSamePlanVersionRequest()
    {
        // Arrange
        var plan = new ControlEvaluationPlan(TenantId, ProgramId, ControlId);
        var versionId = Uuid.CreateVersion4();

        // Act
        var first = plan.Define(0, ControlVersionId, versionId,
            "Management reviews access.", Steps, true,
            AuthorMemberId, Author, ChangedAt);
        var retry = plan.Define(0, ControlVersionId, versionId,
            "Management reviews access.", Steps, true,
            AuthorMemberId, Author, ChangedAt.AddMinutes(1));
        var conflictingRetry = plan.Define(0, ControlVersionId, versionId,
            "Management reviews access.",
            [new("design", "inspection", [new("record", "other", "version/1")], "Different.")],
            true, AuthorMemberId, Author, ChangedAt.AddMinutes(2));

        // Assert
        Assert.Null(first);
        Assert.Null(retry);
        Assert.Equal(CommandFailureCode.StateConflict, conflictingRetry!.Code);
        Assert.Single(plan.ReadVersions());
    }

    [Fact]
    public void ShouldRetainTypedExactScopeVersionsGivenPlanDefinition()
    {
        // Arrange
        var plan = new ControlEvaluationPlan(TenantId, ProgramId, ControlId);
        var scopedItems = new[]
        {
            new EvaluationInspectedItem("boundary", "boundary/abc", "version/3"),
            new EvaluationInspectedItem("commitment", "commitment/xyz", "version/2"),
            new EvaluationInspectedItem("risk", "risk/123", "version/5"),
            new EvaluationInspectedItem("criterion", "criterion/CC6.1", "edition/2026"),
            new EvaluationInspectedItem("control", ControlId.ToString(), ControlVersionId.ToString()),
            new EvaluationInspectedItem("policy", "policy/access-review", "version/4"),
            new EvaluationInspectedItem("provider", "provider/abc", "revision/8"),
            new EvaluationInspectedItem("evidence", "artifact/abc", "sha256:ab12"),
        };

        // Act
        var failure = plan.Define(0, ControlVersionId, Uuid.CreateVersion4(),
            "Evaluate the complete control context.",
            [new("design", "inspection", scopedItems, "Every applicable source is current.")],
            false, AuthorMemberId, Author, ChangedAt);

        // Assert
        Assert.Null(failure);
        Assert.Equal(scopedItems.Select(static item =>
                (item.Kind, item.Reference, item.Version)),
            plan.CurrentVersion!.Steps[0].InspectedItems.Select(static item =>
                (item.Kind, item.Reference, item.Version)));
    }

    [Fact]
    public void ShouldRejectMismatchedControlReferenceGivenPlanDefinition()
    {
        // Arrange
        var plan = new ControlEvaluationPlan(TenantId, ProgramId, ControlId);

        // Act
        var failure = plan.Define(0, ControlVersionId, Uuid.CreateVersion4(),
            "Evaluate control implementation.",
            [new("implementation", "inspection",
                [new("control", Uuid.CreateVersion4().ToString(), ControlVersionId.ToString())],
                "The control matches the approved version.")], false, AuthorMemberId, Author,
            ChangedAt);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, failure!.Code);
    }
}
