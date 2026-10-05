using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkQueueReadConsistencyTests
{
    [Fact]
    public async Task ShouldRejectLaggingEvidenceProjectionGivenCurrentTenantCheckpoint()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await SeedEvidenceRequestAsync(fixture);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var evidence = new ProjectedWorkItems(ProjectionCheckpoint.Start);
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events, [evidence]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.False(captured.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, captured.Error.Kind);
        Assert.True(captured.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldAcceptEvidenceProjectionAtSourceCheckpointGivenNoPendingEvents()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await SeedEvidenceRequestAsync(fixture);
        var evidenceCheckpoint = await ReadCheckpointAsync(events, fixture.TenantId,
            "evidence-requests");
        var evidence = new ProjectedWorkItems(evidenceCheckpoint);
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events, [evidence]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);
        var confirmed = captured.IsSuccess
            ? await consistency.ConfirmUnchangedAndCaughtUpAsync(fixture.TenantId,
                captured.Value, CancellationToken.None)
            : Result.Failure(captured.Error);

        // Assert
        Assert.True(captured.IsSuccess);
        Assert.True(confirmed.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectLaggingCorrectiveActionProjectionGivenCurrentTenantCheckpoint()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(5));
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var correctiveActions = new ProjectedWorkItems(ProjectionCheckpoint.Start,
            area: "remediation", kind: WorkSource.CorrectiveAction,
            projectorName: "TestCorrectiveActionWorkItems");
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events,
            [correctiveActions]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.False(captured.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, captured.Error.Kind);
        Assert.True(captured.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectLaggingRiskGovernanceProjectionGivenCurrentTenantCheckpoint()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskGovernanceLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.AddTreatmentAction(Uuid.CreateVersion4(), 0,
                    Uuid.CreateVersion4(), "mitigate", "Enforce MFA", "MFA is required",
                    "Identity provider policy export.", fixture.Today.AddDays(5),
                    fixture.OwnerMemberId, [],
                    ActorReference.ForMember(fixture.LeadMemberId, "Lead"), now));
                return Result.Success;
            });
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var riskWork = new ProjectedWorkItems(ProjectionCheckpoint.Start,
            area: "risk-governance", kind: WorkSource.RiskTreatmentAction,
            projectorName: "TestRiskWorkItems", kinds:
            [
                WorkSource.RiskTreatmentAction,
                WorkSource.RiskTreatmentActionReview,
                WorkSource.RiskControlTreatmentReview,
            ]);
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events, [riskWork]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.False(captured.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, captured.Error.Kind);
        Assert.True(captured.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectLaggingControlEvaluationProjectionGivenCurrentTenantCheckpoint()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await SubmitControlEvaluationAsync(fixture);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var evaluationWork = new ProjectedWorkItems(ProjectionCheckpoint.Start,
            area: "control-evaluations", kind: WorkSource.ControlEvaluationReview,
            projectorName: "TestControlEvaluationWorkItems");
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events,
            [evaluationWork]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.False(captured.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, captured.Error.Kind);
        Assert.True(captured.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectLaggingCriterionApplicabilityProjectionGivenCurrentTenantCheckpoint()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await SeedCriterionApplicabilityProposalAsync(fixture);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var applicabilityWork = new ProjectedWorkItems(ProjectionCheckpoint.Start,
            area: "criterion-applicability", kind: WorkSource.CriterionApplicabilityReview,
            projectorName: "TestCriterionApplicabilityWorkItems");
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events,
            [applicabilityWork]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.False(captured.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, captured.Error.Kind);
        Assert.True(captured.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectLaggingControlMappingProjectionGivenCurrentTenantCheckpoint()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await SeedControlMappingProposalAsync(fixture);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var mappingWork = new ProjectedWorkItems(ProjectionCheckpoint.Start,
            area: "control-criterion-mappings", kind: WorkSource.ControlCriterionMappingReview,
            projectorName: "TestControlMappingWorkItems");
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events, [mappingWork]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.False(captured.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, captured.Error.Kind);
        Assert.True(captured.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectChangedCorrectiveActionProjectionGivenReadFence()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var correctiveActions = new ProjectedWorkItems(ProjectionCheckpoint.Start,
            area: "remediation", kind: WorkSource.CorrectiveAction,
            projectorName: "TestCorrectiveActionWorkItems");
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events,
            [correctiveActions]);
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);
        Assert.True(captured.IsSuccess);
        correctiveActions.Checkpoint = new ProjectionCheckpoint(new EventCursor("advanced"));

        // Act
        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(fixture.TenantId,
            captured.Value, CancellationToken.None);

        // Assert
        Assert.False(confirmed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, confirmed.Error.Kind);
        Assert.True(confirmed.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldListProjectedEvidenceWorkGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var evidenceCheckpoint = await ReadCheckpointAsync(events, fixture.TenantId,
            "evidence-requests");
        var requestId = Uuid.CreateVersion4();
        var candidate = new WorkCandidate(WorkCandidate.IdFor(requestId, "evidence_request"),
            "evidence_request", requestId, null, null, "Quarterly access export",
            "Upload the approved access export.", fixture.Today.AddDays(5), null, "fulfil",
            $"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
            $"evidence-requests/{requestId}/fulfilments",
            new OperatingHolder(OperatingAuthority.MemberHolder, fixture.OwnerMemberId), null,
            new HashSet<Uuid>(), DateTimeOffset.UtcNow);
        var evidence = new ProjectedWorkItems(evidenceCheckpoint, [candidate]);
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries,
            events, [evidence]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [evidence]);
        var actor = new OperationsActor(fixture.OwnerUserId, fixture.OwnerMemberId, "Owner");

        // Act
        var read = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actor, 30,
            CancellationToken.None);

        // Assert
        Assert.True(read.IsSuccess);
        var entry = Assert.Single(read.Value.Entries,
            item => item.Candidate.Kind == "evidence_request");
        Assert.Equal(requestId, entry.Candidate.SourceId);
        Assert.Equal("Quarterly access export", entry.Item.Summary);
    }

    [Fact]
    public async Task ShouldListProjectedCorrectiveActionWithoutLiveDuplicateGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(5));
        var action = Assert.Single(finding.CorrectiveActions);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var checkpoint = await ReadCheckpointAsync(events, fixture.TenantId, "remediation");
        var candidate = new WorkCandidate(WorkCandidate.IdFor(action.ActionId,
                WorkSource.CorrectiveAction), WorkSource.CorrectiveAction, action.ActionId,
            null, finding.FindingId, action.Description,
            $"Corrective action for finding \"{finding.Title}\".", action.DueOn,
            RemediationLedger.Materiality(finding.Severity), "complete",
            $"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
            $"findings/{finding.FindingId}/corrective-actions/{action.ActionId}/completions",
            new OperatingHolder(OperatingAuthority.MemberHolder, action.OwnerMemberId), null,
            new HashSet<Uuid>(), action.AddedAt);
        var correctiveActions = new ProjectedWorkItems(checkpoint, [candidate], "remediation",
            WorkSource.CorrectiveAction, "TestCorrectiveActionWorkItems");
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events,
            [correctiveActions]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [correctiveActions]);
        var actor = new OperationsActor(fixture.OwnerUserId, fixture.OwnerMemberId, "Owner");

        // Act
        var read = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actor, 30,
            CancellationToken.None);

        // Assert
        Assert.True(read.IsSuccess);
        var entry = Assert.Single(read.Value.Entries,
            item => item.Candidate.Kind == WorkSource.CorrectiveAction);
        Assert.Equal(action.ActionId, entry.Candidate.SourceId);
        Assert.Equal(action.Description, entry.Item.Summary);
    }

    [Fact]
    public async Task ShouldListProjectedRiskActionWithoutLiveDuplicateGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var riskId = Uuid.CreateVersion4();
        var actionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = ActorReference.ForMember(fixture.LeadMemberId, "Lead");
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskGovernanceLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.AddTreatmentAction(riskId, 0, actionId, "mitigate",
                    "Enforce MFA", "MFA is required", "Identity provider policy export.",
                    fixture.Today.AddDays(5), fixture.OwnerMemberId, [], actor, now));
                return Result.Success;
            });
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var checkpoint = await ReadCheckpointAsync(events, fixture.TenantId, "risk-governance");
        var candidate = new WorkCandidate(WorkSource.RiskTreatmentWorkItemId(fixture.ProgramId,
                riskId, actionId, WorkSource.RiskTreatmentAction), WorkSource.RiskTreatmentAction,
            actionId,
            null, null, "Enforce MFA", "Treatment work for a risk. Target state: MFA is required",
            fixture.Today.AddDays(5), null, "complete",
            $"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
            $"risks/{riskId}/treatment-actions/{actionId}/completions",
            new OperatingHolder(OperatingAuthority.MemberHolder, fixture.OwnerMemberId), null,
            new HashSet<Uuid>(), now);
        var riskWork = new ProjectedWorkItems(checkpoint, [candidate], "risk-governance",
            WorkSource.RiskTreatmentAction, "TestRiskWorkItems",
            [WorkSource.RiskTreatmentAction, WorkSource.RiskTreatmentActionReview,
                WorkSource.RiskControlTreatmentReview]);
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events, [riskWork]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [riskWork]);
        var actorContext = new OperationsActor(fixture.OwnerUserId, fixture.OwnerMemberId, "Owner");

        // Act
        var read = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actorContext, 30,
            CancellationToken.None);

        // Assert
        Assert.True(read.IsSuccess);
        var entry = Assert.Single(read.Value.Entries,
            item => item.Candidate.Kind == WorkSource.RiskTreatmentAction);
        Assert.Equal(actionId, entry.Candidate.SourceId);
        Assert.Equal("Enforce MFA", entry.Item.Summary);
    }

    [Fact]
    public async Task ShouldListProjectedControlEvaluationReviewWithoutLiveDuplicateGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await SubmitControlEvaluationAsync(fixture);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var checkpoint = await ReadCheckpointAsync(events, fixture.TenantId, "control-evaluations");
        var identity = Uuid.CreateVersion5(evaluation.EvaluationId,
            $"control-evaluation-review\n{evaluation.Round}");
        var candidate = new WorkCandidate(WorkCandidate.IdFor(identity,
                WorkSource.ControlEvaluationReview), WorkSource.ControlEvaluationReview,
            evaluation.EvaluationId, evaluation.ControlId, null, "Review control evaluation",
            $"Control evaluation round {evaluation.Round} is awaiting independent review.", null,
            null, "review",
            $"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/controls/" +
            $"{evaluation.ControlId}/evaluations/{evaluation.EvaluationId}/reviews",
            new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, fixture.ProgramId), null,
            new HashSet<Uuid> { evaluation.EvaluatorMemberId }, evaluation.Submissions[^1].SubmittedAt);
        var evaluationWork = new ProjectedWorkItems(checkpoint, [candidate], "control-evaluations",
            WorkSource.ControlEvaluationReview, "TestControlEvaluationWorkItems");
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events,
            [evaluationWork]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [evaluationWork]);
        var actor = new OperationsActor(fixture.LeadUserId, fixture.LeadMemberId, "Lead");

        // Act
        var read = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actor, 30,
            CancellationToken.None);

        // Assert
        Assert.True(read.IsSuccess);
        var entry = Assert.Single(read.Value.Entries,
            item => item.Candidate.Kind == WorkSource.ControlEvaluationReview);
        Assert.Equal(evaluation.EvaluationId, entry.Candidate.SourceId);
        Assert.Equal("Review control evaluation", entry.Item.Summary);
    }

    [Fact]
    public async Task ShouldListProjectedCriterionApplicabilityReviewWithoutLiveDuplicateGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var (decisionId, proposedAt) =
            await SeedCriterionApplicabilityProposalAsync(fixture);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var checkpoint = await ReadCheckpointAsync(events, fixture.TenantId,
            "criterion-applicability");
        var identity = Uuid.CreateVersion5(decisionId, "criterion-applicability-review\n1");
        var candidate = new WorkCandidate(WorkCandidate.IdFor(identity,
                WorkSource.CriterionApplicabilityReview), WorkSource.CriterionApplicabilityReview,
            decisionId, null, null, "Review not-applicable proposal for AC-2.4",
            "A criterion not-applicable proposal is awaiting independent review.", null, null,
            "review", $"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
            $"criterion-applicability/{decisionId}/reviews",
            new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, fixture.ProgramId), null,
            new HashSet<Uuid> { fixture.OwnerMemberId }, proposedAt);
        var applicabilityWork = new ProjectedWorkItems(checkpoint, [candidate],
            "criterion-applicability", WorkSource.CriterionApplicabilityReview,
            "TestCriterionApplicabilityWorkItems");
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events,
            [applicabilityWork]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [applicabilityWork]);
        var actor = new OperationsActor(fixture.LeadUserId, fixture.LeadMemberId, "Lead");

        // Act
        var read = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actor, 30,
            CancellationToken.None);

        // Assert
        Assert.True(read.IsSuccess);
        var entry = Assert.Single(read.Value.Entries,
            item => item.Candidate.Kind == WorkSource.CriterionApplicabilityReview);
        Assert.Equal(decisionId, entry.Candidate.SourceId);
        Assert.Null(entry.Candidate.ControlId);
        Assert.Equal("Review not-applicable proposal for AC-2.4", entry.Item.Summary);
    }

    [Fact]
    public async Task ShouldListProjectedControlMappingReviewWithoutLiveDuplicateGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var (mappingId, proposedAt) = await SeedControlMappingProposalAsync(fixture);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var checkpoint = await ReadCheckpointAsync(events, fixture.TenantId,
            "control-criterion-mappings");
        var candidate = WorkSource.ControlCriterionMappingReviewCandidate(fixture.TenantId,
            fixture.ProgramId, fixture.ControlId, mappingId, "AC-2.4", fixture.OwnerMemberId, 1,
            proposedAt);
        var mappingWork = new ProjectedWorkItems(checkpoint, [candidate],
            "control-criterion-mappings", WorkSource.ControlCriterionMappingReview,
            "TestControlMappingWorkItems");
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events, [mappingWork]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [mappingWork]);
        var actor = new OperationsActor(fixture.LeadUserId, fixture.LeadMemberId, "Lead");

        // Act
        var read = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actor, 30,
            CancellationToken.None);

        // Assert
        Assert.True(read.IsSuccess);
        var entry = Assert.Single(read.Value.Entries,
            item => item.Candidate.Kind == WorkSource.ControlCriterionMappingReview);
        Assert.Equal(mappingId, entry.Candidate.SourceId);
        Assert.Equal(fixture.ControlId, entry.Candidate.ControlId);
        Assert.Equal("Review control mapping for AC-2.4", entry.Item.Summary);
    }

    static async Task<ControlEvaluationView> SubmitControlEvaluationAsync(OperationsFixture fixture)
    {
        var plan = await fixture.GetOrDefineEvaluationPlanAsync();
        var evaluationId = Uuid.CreateVersion4();
        var startedAt = DateTimeOffset.UtcNow;
        ControlEvaluationView? submitted = null;
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new ControlEvaluationLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.Start(fixture.ControlId, evaluationId, plan.ControlVersionId,
                    plan.PlanVersionId, plan.Version, plan.Steps, null, fixture.OwnerMemberId,
                    "Owner", startedAt));
                var current = ledger.Read(fixture.ControlId, evaluationId)!;
                foreach (var step in current.Steps)
                {
                    Assert.Null(ledger.RecordStep(fixture.ControlId, evaluationId, step.StepId,
                        current.Revision, ControlEvaluationLedger.Met,
                        "Inspected the exact items.", step.InspectedItems, null, null,
                        fixture.OwnerMemberId, "Owner", startedAt.AddMinutes(1)));
                    current = ledger.Read(fixture.ControlId, evaluationId)!;
                }
                Assert.Null(ledger.Submit(fixture.ControlId, evaluationId, current.Revision,
                    [new("design", ControlEvaluationLedger.Effective, "Design is effective."),
                        new("implementation", ControlEvaluationLedger.Effective,
                            "Implementation is effective."),
                        new("evidence_sufficiency", ControlEvaluationLedger.Effective,
                            "Evidence is sufficient.")], fixture.OwnerMemberId, "Owner",
                    startedAt.AddMinutes(2)));
                submitted = ledger.Read(fixture.ControlId, evaluationId);
                return Result.Success;
            });
        return Assert.IsType<ControlEvaluationView>(submitted);
    }

    static async Task<(Uuid DecisionId, DateTimeOffset ProposedAt)>
        SeedCriterionApplicabilityProposalAsync(OperationsFixture fixture)
    {
        var editionId = Uuid.CreateVersion4();
        var proposedAt = DateTimeOffset.UtcNow;
        var decisionId = CriterionApplicabilityLedger.DecisionIdFor(fixture.ProgramId, editionId,
            "AC-2.4");
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new CriterionApplicabilityLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.Propose(editionId, "AC-2.4", 0,
                    "The organization has no in-scope system for this criterion.",
                    fixture.OwnerMemberId,
                    ActorReference.ForMember(fixture.OwnerMemberId, "Owner"), proposedAt,
                    out _));
                return Result.Success;
            });
        return (decisionId, proposedAt);
    }

    static async Task<(Uuid MappingId, DateTimeOffset ProposedAt)>
        SeedControlMappingProposalAsync(OperationsFixture fixture)
    {
        var editionId = Uuid.CreateVersion4();
        var proposedAt = DateTimeOffset.UtcNow;
        var mappingId = ControlCriterionMappingLedger.MappingIdFor(fixture.ProgramId,
            fixture.ControlId, editionId, "AC-2.4");
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new ControlCriterionMappingLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.Propose(fixture.ControlId, fixture.ControlVersionId, editionId,
                    "AC-2.4", "security", 0, "The control addresses this criterion.",
                    "The control covers the in-scope environment.", fixture.OwnerMemberId,
                    "Owner", proposedAt, out _));
                return Result.Success;
            });
        return (mappingId, proposedAt);
    }

    static async Task SeedEvidenceRequestAsync(OperationsFixture fixture)
    {
        var openedAt = DateTimeOffset.UtcNow;
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new EvidenceRequestLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.OpenRequest(Uuid.CreateVersion4(), "Access export",
                    "Upload the approved access export.", fixture.OwnerMemberId,
                    fixture.Today.AddDays(5), null,
                    ActorReference.ForMember(fixture.LeadMemberId, "Lead"), openedAt));
                return Result.Success;
            });
    }

    static async Task<ProjectionCheckpoint> ReadCheckpointAsync(
        IDomainEventReader events, Uuid tenantId, string area)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(
                           EventStreamPattern.ForPattern(tenantId.ToString(), area),
                           cursor, CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed class ProjectedWorkItems(ProjectionCheckpoint checkpoint,
        IReadOnlyList<WorkCandidate>? candidates = null,
        string area = "evidence-requests", string kind = WorkSource.EvidenceRequest,
        string projectorName = "TestEvidenceWorkItems",
        IReadOnlyCollection<string>? kinds = null)
        : IAccountableWorkItemDirectoryReader
    {
        public string ProjectorName => projectorName;

        public IReadOnlyCollection<string> ProjectedKinds => kinds ?? [kind];

        public EventStreamPattern SourcePattern(Uuid tenantId) =>
            EventStreamPattern.ForPattern(tenantId.ToString(), area);

        public ProjectionCheckpoint Checkpoint { get; set; } = checkpoint;

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(Checkpoint);

        public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
            Uuid programId, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<IReadOnlyList<WorkCandidate>>.Success(candidates ?? []));
    }
}
