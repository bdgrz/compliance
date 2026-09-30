using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class FitzRiskEvaluationDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldProjectCurrentStateAndOrderedHistoryGivenEvaluationEvents()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var riskId = Uuid.CreateVersion4();
        var directory = new FitzRiskEvaluationDirectory(new InMemoryKvClient());
        var events = Events(tenantId, programId, riskId);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            foreach (var domainEvent in events)
                await directory.ApplyAsync(domainEvent);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var current = await directory.GetAsync(tenantId, riskId);
        var history = await directory.ListHistoryAsync(tenantId, riskId, 2, null);
        var rest = await directory.ListHistoryAsync(tenantId, riskId, 2, history.NextCursor);
        var alien = await directory.GetAsync(Uuid.CreateVersion4(), riskId);

        // Assert
        Assert.NotNull(current);
        Assert.Equal(3, current.Revision);
        Assert.Equal("accept", current.Treatment!.Kind);
        Assert.Equal(2, current.Assessments.Count);
        Assert.Equal([1L, 2L], history.Items.Select(item => item.Revision));
        Assert.Equal("assessment_recorded", history.Items[0].Kind);
        Assert.Equal("treatment_chosen", history.Items[1].Kind);
        Assert.Equal("assessment_recorded", Assert.Single(rest.Items).Kind);
        Assert.Null(alien);
    }

    [Fact]
    public async Task ShouldRejectOutOfOrderRevisionGivenMissingPredecessor()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var directory = new FitzRiskEvaluationDirectory(new InMemoryKvClient());
        var events = Events(tenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            Identity(tenantId), ProjectionCheckpoint.Start));

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            await directory.ApplyAsync(events[1]));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    static List<DomainEvent> Events(Uuid tenantId, Uuid programId, Uuid riskId)
    {
        var method = new RiskMethod(tenantId, programId);
        Assert.Null(method.Publish(programId, 0, RiskMethodTests.Scale(), RiskMethodTests.Scale(),
            12, ActorReference.ForMember(Uuid.CreateVersion4(), "Lead"), Now));
        var evaluation = new RiskEvaluation(tenantId, riskId);
        var assessor = Uuid.CreateVersion4();
        Assert.Null(evaluation.RecordAssessment(programId, 0, Uuid.CreateVersion4(),
            method.Current!, "inherent", 4, 4, "High", assessor, "Assessor", Now));
        Assert.Null(evaluation.ChooseTreatment(programId, 1, "accept", "Accept it", assessor,
            "Assessor", Now));
        Assert.Null(evaluation.RecordAssessment(programId, 2, Uuid.CreateVersion4(),
            method.Current!, "residual", 2, 2, "Low", assessor, "Assessor", Now));
        return [.. new AggregateScenario<RiskEvaluation>(evaluation).PendingEvents];
    }

    static CheckpointIdentity Identity(Uuid tenantId) => new("RiskEvaluationDirectoryV1",
        EventStreamPattern.ForPattern(tenantId.ToString(), "risk-evaluations"));
}
