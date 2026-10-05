using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Evaluations;

public sealed class ControlEvaluationStartIdempotencyTests
{
    [Fact]
    public void ShouldAcceptExactRetryAndRejectChangedRequestGivenEvaluationIdReuse()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var controlVersionId = Uuid.CreateVersion4();
        var planVersionId = Uuid.CreateVersion4();
        var evaluationId = Uuid.CreateVersion4();
        var evaluatorMemberId = Uuid.CreateVersion4();
        var actorDisplay = "Evaluator";
        var startedAt = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        IReadOnlyList<EvaluationProcedureStep> steps =
        [
            new("design", "inspection",
                [new EvaluationInspectedItem("boundary", "boundary/abc", "version/1")],
                "Management defined the review cadence."),
        ];
        var ledger = new ControlEvaluationLedger(tenantId, programId);

        // Act
        var first = ledger.Start(controlId, evaluationId, controlVersionId, planVersionId, 1,
            steps, null, evaluatorMemberId, actorDisplay, startedAt);
        var retry = ledger.Start(controlId, evaluationId, controlVersionId, planVersionId, 1,
            steps, null, evaluatorMemberId, actorDisplay, startedAt.AddMinutes(1));
        var changedProcedure = ledger.Start(controlId, evaluationId, controlVersionId,
            planVersionId, 1,
            [new("design", "inspection",
                [new EvaluationInspectedItem("record", "boundary/changed", "version/1")],
                "A different procedure.")], null, evaluatorMemberId, actorDisplay, startedAt);

        // Assert
        Assert.Null(first);
        Assert.Null(retry);
        Assert.Equal(CommandFailureCode.StateConflict, changedProcedure!.Code);
    }
}
