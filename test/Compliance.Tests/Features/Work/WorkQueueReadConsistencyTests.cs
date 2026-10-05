using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Remediation;
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
        string projectorName = "TestEvidenceWorkItems")
        : IAccountableWorkItemDirectoryReader
    {
        public string ProjectorName => projectorName;

        public IReadOnlyCollection<string> ProjectedKinds => [kind];

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
