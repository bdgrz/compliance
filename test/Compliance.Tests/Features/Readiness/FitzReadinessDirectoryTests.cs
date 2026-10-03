using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Readiness;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Readiness;

public sealed class FitzReadinessDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReadCurrentStateAndIsolateTenantGivenReadinessEvents()
    {
        // Arrange
        var directory = new FitzReadinessDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var firstAssessmentId = Uuid.CreateVersion4();
        var secondAssessmentId = Uuid.CreateVersion4();
        var otherAssessmentId = Uuid.CreateVersion4();
        var gapId = Uuid.CreateVersion4();
        var runnerId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(runnerId, "Readiness runner");
        var gap = new ReadinessGapView(gapId, "missing_input", "risks", "risk_reviewed",
            "No risk review is available.", []);
        var first = Assessment(tenantId, programId, firstAssessmentId, 1, [gap], runnerId, actor);
        var plan = new ReadinessGapPlanned(tenantId, programId, 2,
            new ReadinessGapPlanView(gapId, ownerId, new DateOnly(2027, 3, 31),
                "Complete the risk review.", actor, Now.AddMinutes(1)));
        var decision = new ReadinessDecisionRecorded(tenantId, programId, 3,
            new ReadinessDecisionView(Uuid.CreateVersion4(), firstAssessmentId, "proceed",
                "The gap is owned.", ownerId, actor, Now.AddMinutes(2), null));
        var typeI = new TypeIEntryDecisionView(Uuid.CreateVersion4(), firstAssessmentId,
            ReadinessRules.Version, Now, first.InputFingerprint, "defer", "Review first.", [],
            ownerId, actor, Now.AddMinutes(3), null);
        var typeIEvent = new TypeIEntryDecisionRecorded(tenantId, programId, 4, typeI);
        var annotation = new ReadinessAnnotationView(Uuid.CreateVersion4(), firstAssessmentId,
            gapId, "Please attach supporting notes.", ownerId, actor, Now.AddMinutes(4));
        var annotationEvent = new ReadinessGapAnnotated(tenantId, programId, 5, annotation);
        var second = Assessment(tenantId, programId, secondAssessmentId, 6, [], runnerId, actor);
        var events = new DomainEvent[] { first, plan, decision, typeIEvent, annotationEvent, second };

        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            foreach (var domainEvent in events)
                await directory.ApplyAsync(domainEvent);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        await using (var replay = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            foreach (var domainEvent in events)
                await directory.ApplyAsync(domainEvent);
            await replay.CommitAsync(ProjectionCheckpoint.Start);
        }
        await using (var otherBatch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(otherTenantId), ProjectionCheckpoint.Start)))
        {
            var otherRunnerId = Uuid.CreateVersion4();
            var otherAssessment = Assessment(otherTenantId, programId, otherAssessmentId,
                1, [], otherRunnerId, ActorReference.ForMember(otherRunnerId, "Other runner"));
            await directory.ApplyAsync(otherAssessment);
            await otherBatch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var current = await directory.GetAssessmentAsync(tenantId, programId, firstAssessmentId);
        var summaries = await directory.ListAssessmentsAsync(tenantId, programId, 1, null);
        var olderSummary = await directory.ListAssessmentsAsync(tenantId, programId, 1,
            summaries.Value.NextCursor);
        var gaps = await directory.ListGapsAsync(tenantId, programId, firstAssessmentId,
            "planned", 10, null, ownerId, null, null, null);
        var annotations = await directory.ListAnnotationsAsync(tenantId, programId,
            firstAssessmentId, 10, null);
        var typeIDecisions = await directory.ListTypeIEntryDecisionsAsync(tenantId, programId,
            10, null);

        // Assert
        Assert.NotNull(current);
        Assert.Equal(6, current.Revision);
        Assert.Equal(plan.Plan, Assert.Single(current.Gaps).Plan);
        Assert.Equal(decision.Decision, current.Decision);
        Assert.NotNull(current.TypeIEntryDecision);
        Assert.Equal(typeI.DecisionId, current.TypeIEntryDecision.DecisionId);
        Assert.Equal(typeI.Outcome, current.TypeIEntryDecision.Outcome);
        Assert.Equal(secondAssessmentId, Assert.Single(summaries.Value.Items).AssessmentId);
        Assert.Equal(firstAssessmentId, Assert.Single(olderSummary.Value.Items).AssessmentId);
        Assert.Equal("proceed", olderSummary.Value.Items[0].DecisionOutcome);
        Assert.Equal(gapId, Assert.Single(gaps.Value.Items).GapId);
        Assert.False(Assert.Single(annotations.Value.Items).Current);
        Assert.Equal(typeI.DecisionId, Assert.Single(typeIDecisions.Value.Items).DecisionId);
        Assert.Null(await directory.GetAssessmentAsync(otherTenantId, programId, firstAssessmentId));
        var otherTenantAssessments = (await directory.ListAssessmentsAsync(otherTenantId,
            programId, 10, null)).Value.Items;
        Assert.Equal(otherAssessmentId, Assert.Single(otherTenantAssessments).AssessmentId);
        Assert.DoesNotContain(otherTenantAssessments,
            summary => summary.AssessmentId == firstAssessmentId);
    }

    static ReadinessAssessmentRecorded Assessment(Uuid tenantId, Uuid programId,
        Uuid assessmentId, long revision, IReadOnlyList<ReadinessGapView> gaps,
        Uuid runnerMemberId, ActorReference actor) => new(tenantId, programId, revision, assessmentId,
        ReadinessRules.Version, Now, null, $"fingerprint-{assessmentId}", [], [], gaps,
        runnerMemberId, actor, Now);

    static CheckpointIdentity Identity(Uuid tenantId) => new(FitzReadinessDirectory.ProjectorName,
        EventStreamPattern.ForPattern(tenantId.ToString(), ReadinessLedger.Area));
}
