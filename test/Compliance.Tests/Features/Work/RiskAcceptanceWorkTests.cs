using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class RiskAcceptanceWorkTests
{
    [Fact]
    public async Task ShouldQueueAcceptanceForEligibleIndependentApproverGivenPendingResidualRisk()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var riskId = Uuid.CreateVersion4();
        var residualAssessmentId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var method = await SeedMethodAsync(fixture, 20, now);
        fixture.Risks.Add(new RiskDraftView(fixture.TenantId, fixture.ProgramId, riskId,
            "R-ACCEPT", 1, "assessed", "resolved",
            new RiskDraftContent("Data exposure", "Customer records could be exposed.",
                "Loss of customer trust.", null), fixture.LeadMemberId, "Lead", now));
        await SeedRiskOwnerAsync(fixture, riskId, now);
        fixture.Permissions.RiskApprovers.Add((fixture.ApproverMemberId,
            RbacPermissions.RiskAcceptComplianceLead));
        fixture.Permissions.RiskApprovers.Add((fixture.ApproverMemberId,
            RbacPermissions.RiskAcceptExecutive));
        fixture.Permissions.RiskApprovers.Add((fixture.LeadMemberId,
            RbacPermissions.RiskAcceptComplianceLead));
        fixture.Permissions.RiskApprovers.Add((fixture.LeadMemberId,
            RbacPermissions.RiskAcceptExecutive));
        fixture.Permissions.RiskApprovers.Add((fixture.OwnerMemberId,
            RbacPermissions.RiskAcceptComplianceLead));
        fixture.Permissions.RiskApprovers.Add((fixture.OwnerMemberId,
            RbacPermissions.RiskAcceptExecutive));
        await SeedPendingAcceptanceAsync(fixture, riskId, residualAssessmentId, method, now);

        // Act
        var approverQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var assessorQueue = await fixture.AsAsync(fixture.LeadUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var ownerQueue = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        var item = Assert.Single(approverQueue.Items);
        Assert.Equal("risk_acceptance", item.Kind);
        Assert.Equal(riskId, item.SourceId);
        Assert.Equal("accept", item.NextAction);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"risks/{riskId}/acceptances", item.ActionPath);
        Assert.Empty(assessorQueue.Items);
        Assert.Empty(ownerQueue.Items);
    }

    [Fact]
    public async Task ShouldRequireExecutiveGivenResidualRiskAboveAppetite()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var riskId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var method = await SeedMethodAsync(fixture, 20, now);
        fixture.Risks.Add(new RiskDraftView(fixture.TenantId, fixture.ProgramId, riskId,
            "R-EXEC", 1, "assessed", "resolved",
            new RiskDraftContent("Material data exposure", "Sensitive records are exposed.",
                "Severe customer impact.", null), fixture.LeadMemberId, "Lead", now));
        fixture.Permissions.RiskApprovers.Add((fixture.ApproverMemberId,
            RbacPermissions.RiskAcceptExecutive));
        fixture.Permissions.RiskApprovers.Add((fixture.ReviewerMemberId,
            RbacPermissions.RiskAcceptComplianceLead));
        await SeedPendingAcceptanceAsync(fixture, riskId, Uuid.CreateVersion4(), method, now,
            likelihood: 5, impact: 5);

        // Act
        var executiveQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var complianceLeadQueue = await fixture.AsAsync(fixture.ReviewerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        var item = Assert.Single(executiveQueue.Items);
        Assert.Equal(OperatingAuthority.RiskExecutiveHolder, item.Responsible.Kind);
        Assert.Contains("executive", item.Reason, StringComparison.Ordinal);
        Assert.Empty(complianceLeadQueue.Items);
    }

    [Fact]
    public async Task ShouldRemoveAcceptanceWorkGivenCurrentResidualRiskAccepted()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var riskId = Uuid.CreateVersion4();
        var residualAssessmentId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var method = await SeedMethodAsync(fixture, 20, now);
        fixture.Risks.Add(new RiskDraftView(fixture.TenantId, fixture.ProgramId, riskId,
            "R-ACTIVE", 1, "assessed", "resolved",
            new RiskDraftContent("Contained exposure", "Records are protected.",
                "Limited customer impact.", null), fixture.LeadMemberId, "Lead", now));
        fixture.Permissions.RiskApprovers.Add((fixture.ApproverMemberId,
            RbacPermissions.RiskAcceptComplianceLead));
        await SeedPendingAcceptanceAsync(fixture, riskId, residualAssessmentId, method, now);

        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskEvaluation(fixture.TenantId, riskId), evaluation =>
            {
                Assert.Null(evaluation.Accept(fixture.ProgramId, 3, Uuid.CreateVersion4(),
                    residualAssessmentId, method, "compliance_lead", true,
                    now.AddMonths(6), "The residual exposure is accepted.",
                    fixture.ApproverMemberId, "Approver", now.AddMinutes(3)));
                return Result.Success;
            });

        // Act
        var queue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        Assert.DoesNotContain(queue.Items, item => item.Kind == "risk_acceptance");
    }

    [Fact]
    public async Task ShouldNotQueueAcceptanceGivenTreatmentChangedAfterResidualAssessment()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var riskId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var method = await SeedMethodAsync(fixture, 20, now);
        fixture.Risks.Add(new RiskDraftView(fixture.TenantId, fixture.ProgramId, riskId,
            "R-STALE", 1, "assessed", "resolved",
            new RiskDraftContent("Data exposure", "Customer records could be exposed.",
                "Loss of customer trust.", null), fixture.LeadMemberId, "Lead", now));
        fixture.Permissions.RiskApprovers.Add((fixture.ApproverMemberId,
            RbacPermissions.RiskAcceptComplianceLead));
        await SeedTreatmentChangeAfterResidualAsync(fixture, riskId, method, now);

        // Act
        var queue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        Assert.DoesNotContain(queue.Items, item => item.Kind == "risk_acceptance");
    }

    [Fact]
    public async Task ShouldNotDuplicateRiskAcceptanceGivenProjectionAndLiveSource()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var riskId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var method = await SeedMethodAsync(fixture, 20, now);
        fixture.Risks.Add(new RiskDraftView(fixture.TenantId, fixture.ProgramId, riskId,
            "R-PROJECTED", 1, "assessed", "resolved",
            new RiskDraftContent("Data exposure", "Customer records could be exposed.",
                "Loss of customer trust.", null), fixture.LeadMemberId, "Lead", now));
        fixture.Permissions.RiskApprovers.Add((fixture.ApproverMemberId,
            RbacPermissions.RiskAcceptComplianceLead));
        await SeedPendingAcceptanceAsync(fixture, riskId, Uuid.CreateVersion4(), method, now);
        await fixture.CatchUpBoundaryDirectoryAsync();

        await using var scope = fixture.Provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var source = await RiskAcceptanceWork.LoadAsync(reader, fixture.Risks, null,
            fixture.TenantId, fixture.ProgramId, now, null, CancellationToken.None);
        var candidate = Assert.Single(source.Value);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var projected = new ProjectedRiskAcceptanceWorkItems(candidate,
            await ReadRiskEvaluationCheckpointAsync(events, fixture.TenantId));
        var consistency = new WorkQueueReadConsistency(events, [projected]);
        var queue = new WorkQueueReader(reader,
            scope.ServiceProvider.GetRequiredService<OperatingAuthority>(), TimeProvider.System,
            consistency, risks: fixture.Risks, accountableWorkItems: [projected]);
        var actor = new OperationsActor(fixture.ApproverUserId, fixture.ApproverMemberId,
            "Approver");

        // Act
        var result = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actor, 30,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Entries, entry => entry.Item.Kind == "risk_acceptance");
    }

    [Fact]
    public async Task ShouldBuildRiskAcceptanceWorkFromRiskEvaluationProjectionGivenPendingResidualRisk()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var riskId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var method = await SeedMethodAsync(fixture, 20, now);
        fixture.Risks.Add(new RiskDraftView(fixture.TenantId, fixture.ProgramId, riskId,
            "R-PROJECTED", 1, "assessed", "resolved",
            new RiskDraftContent("Data exposure", "Customer records could be exposed.",
                "Loss of customer trust.", null), fixture.LeadMemberId, "Lead", now));
        await SeedPendingAcceptanceAsync(fixture, riskId, Uuid.CreateVersion4(), method, now);

        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var evaluations = new FitzRiskEvaluationDirectory(new InMemoryKvClient());
        await ProjectRiskEvaluationsAsync(evaluations, events, fixture.TenantId);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var workItems = new RiskAcceptanceWorkItemDirectory(reader, fixture.Risks, null,
            evaluations);
        Assert.Equal(await ReadRiskEvaluationCheckpointAsync(events, fixture.TenantId),
            await workItems.LoadCheckpointAsync(fixture.TenantId));

        // Act
        var result = await workItems.LoadProgramAsync(fixture.TenantId, fixture.ProgramId, now,
            DateOnly.MaxValue, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var candidate = Assert.Single(result.Value);
        Assert.Equal("risk_acceptance", candidate.Kind);
        Assert.Equal(riskId, candidate.SourceId);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"risks/{riskId}/acceptances", candidate.ActionPath);
    }

    [Fact]
    public async Task ShouldRejectLaggingRiskAcceptanceProjectionGivenCurrentEvaluationEvents()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var riskId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var method = await SeedMethodAsync(fixture, 20, now);
        fixture.Risks.Add(new RiskDraftView(fixture.TenantId, fixture.ProgramId, riskId,
            "R-LAGGING", 1, "assessed", "resolved",
            new RiskDraftContent("Data exposure", "Customer records could be exposed.",
                "Loss of customer trust.", null), fixture.LeadMemberId, "Lead", now));
        await SeedPendingAcceptanceAsync(fixture, riskId, Uuid.CreateVersion4(), method, now);
        await fixture.CatchUpBoundaryDirectoryAsync();

        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var evaluations = new FitzRiskEvaluationDirectory(new InMemoryKvClient());
        await using var scope = fixture.Provider.CreateAsyncScope();
        var workItems = new RiskAcceptanceWorkItemDirectory(
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(), fixture.Risks, null,
            evaluations);
        var consistency = new WorkQueueReadConsistency(events, [workItems]);

        // Act
        var result = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    static async Task<RiskMethodVersionView> SeedMethodAsync(OperationsFixture fixture,
        int? appetiteThreshold, DateTimeOffset now)
    {
        RiskMethodVersionView? methodVersion = null;
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskMethod(fixture.TenantId, fixture.ProgramId), method =>
            {
                Assert.Null(method.Publish(fixture.ProgramId, 0,
                    ["rare", "unlikely", "possible", "likely", "almost certain"],
                    ["low", "minor", "moderate", "major", "severe"], appetiteThreshold,
                    ActorReference.ForMember(fixture.LeadMemberId, "Lead"), now));
                methodVersion = method.Current;
                return Result.Success;
            });
        return methodVersion!;
    }

    static Task SeedRiskOwnerAsync(OperationsFixture fixture, Uuid riskId,
        DateTimeOffset now) =>
        ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskGovernanceLedger(fixture.TenantId, fixture.ProgramId), governance =>
            {
                Assert.Null(governance.AssignOwner(riskId, 0, fixture.PersonId,
                    fixture.OwnerMemberId, "Owns the service risk.",
                    ActorReference.ForMember(fixture.LeadMemberId, "Lead"), now));
                return Result.Success;
            });

    static Task SeedPendingAcceptanceAsync(OperationsFixture fixture, Uuid riskId,
        Uuid residualAssessmentId, RiskMethodVersionView method, DateTimeOffset now,
        int likelihood = 3, int impact = 3) =>
        ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskEvaluation(fixture.TenantId, riskId), evaluation =>
            {
                Assert.Null(evaluation.RecordAssessment(fixture.ProgramId, 0,
                    Uuid.CreateVersion4(), method, RiskEvaluation.Inherent, 2, 2,
                    "The baseline exposure is limited.", fixture.LeadMemberId, "Lead", now));
                Assert.Null(evaluation.ChooseTreatment(fixture.ProgramId, 1, "accept",
                    "The residual exposure is within appetite.", fixture.LeadMemberId, "Lead",
                    now.AddMinutes(1)));
                Assert.Null(evaluation.RecordAssessment(fixture.ProgramId, 2,
                    residualAssessmentId, method, RiskEvaluation.Residual, likelihood, impact,
                    "The residual exposure remains within appetite.", fixture.LeadMemberId,
                    "Lead", now.AddMinutes(2)));
                return Result.Success;
            });

    static Task SeedTreatmentChangeAfterResidualAsync(OperationsFixture fixture, Uuid riskId,
        RiskMethodVersionView method, DateTimeOffset now) =>
        ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskEvaluation(fixture.TenantId, riskId), evaluation =>
            {
                Assert.Null(evaluation.RecordAssessment(fixture.ProgramId, 0,
                    Uuid.CreateVersion4(), method, RiskEvaluation.Inherent, 2, 2,
                    "The baseline exposure is limited.", fixture.LeadMemberId, "Lead", now));
                Assert.Null(evaluation.ChooseTreatment(fixture.ProgramId, 1, "accept",
                    "The residual exposure may be accepted.", fixture.LeadMemberId, "Lead",
                    now.AddMinutes(1)));
                Assert.Null(evaluation.RecordAssessment(fixture.ProgramId, 2,
                    Uuid.CreateVersion4(), method, RiskEvaluation.Residual, 3, 3,
                    "The residual exposure remains within appetite.", fixture.LeadMemberId,
                    "Lead", now.AddMinutes(2)));
                Assert.Null(evaluation.ChooseTreatment(fixture.ProgramId, 3, "transfer",
                    "The plan changed after the residual assessment.", fixture.LeadMemberId,
                    "Lead", now.AddMinutes(3)));
                Assert.Null(evaluation.ChooseTreatment(fixture.ProgramId, 4, "accept",
                    "Acceptance is considered again after the plan change.",
                    fixture.LeadMemberId, "Lead", now.AddMinutes(4)));
                return Result.Success;
            });

    static async Task<ProjectionCheckpoint> ReadRiskEvaluationCheckpointAsync(
        IDomainEventReader events, Uuid tenantId)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(
                           tenantId.ToString(), "risk-evaluations"), cursor,
                           CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    static async Task ProjectRiskEvaluationsAsync(FitzRiskEvaluationDirectory directory,
        IDomainEventReader events, Uuid tenantId)
    {
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString(), "risk-evaluations");
        var identity = new CheckpointIdentity(FitzRiskEvaluationDirectory.ProjectorName, pattern);
        var checkpoint = await directory.LoadCheckpointAsync(tenantId);
        await foreach (var record in events.ReadAsync(pattern, checkpoint.Cursor,
                           CancellationToken.None))
        {
            await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                checkpoint));
            await directory.ApplyAsync(record.Event);
            checkpoint = new ProjectionCheckpoint(record.NextCursor);
            await batch.CommitAsync(checkpoint);
        }
    }

    sealed class ProjectedRiskAcceptanceWorkItems(WorkCandidate candidate,
        ProjectionCheckpoint checkpoint) : IAccountableWorkItemDirectoryReader
    {
        static readonly string[] Kinds = [RiskAcceptanceWork.Kind];

        public string ProjectorName => "TestRiskEvaluationWorkItems";

        public IReadOnlyCollection<string> ProjectedKinds => Kinds;

        public EventStreamPattern SourcePattern(Uuid tenantId) =>
            EventStreamPattern.ForPattern(tenantId.ToString(), "risk-evaluations");

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);

        public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
            Uuid programId, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<IReadOnlyList<WorkCandidate>>.Success([candidate]));
    }
}
