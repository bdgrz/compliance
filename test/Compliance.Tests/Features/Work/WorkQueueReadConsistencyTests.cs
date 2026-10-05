using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.AccessControl;
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
        var evidence = new EvidenceWorkItems(ProjectionCheckpoint.Start);
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events, evidence);

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
        var evidenceCheckpoint = await ReadEvidenceCheckpointAsync(events, fixture.TenantId);
        var evidence = new EvidenceWorkItems(evidenceCheckpoint);
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events, evidence);

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
    public async Task ShouldListProjectedEvidenceWorkGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var evidenceCheckpoint = await ReadEvidenceCheckpointAsync(events, fixture.TenantId);
        var requestId = Uuid.CreateVersion4();
        var candidate = new WorkCandidate(WorkCandidate.IdFor(requestId, "evidence_request"),
            "evidence_request", requestId, null, null, "Quarterly access export",
            "Upload the approved access export.", fixture.Today.AddDays(5), null, "fulfil",
            $"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
            $"evidence-requests/{requestId}/fulfilments",
            new OperatingHolder(OperatingAuthority.MemberHolder, fixture.OwnerMemberId), null,
            new HashSet<Uuid>(), DateTimeOffset.UtcNow);
        var evidence = new EvidenceWorkItems(evidenceCheckpoint, [candidate]);
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries,
            events, evidence);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            evidenceWorkItems: evidence);
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

    static async Task<ProjectionCheckpoint> ReadEvidenceCheckpointAsync(
        IDomainEventReader events, Uuid tenantId)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(
                           EventStreamPattern.ForPattern(tenantId.ToString(), "evidence-requests"),
                           cursor, CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed class EvidenceWorkItems(ProjectionCheckpoint checkpoint,
        IReadOnlyList<WorkCandidate>? candidates = null)
        : IEvidenceWorkItemDirectoryReader
    {
        public ProjectionCheckpoint Checkpoint { get; set; } = checkpoint;

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(Checkpoint);

        public ValueTask<long> LoadRevisionAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(0L);

        public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
            Uuid programId, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<IReadOnlyList<WorkCandidate>>.Success(candidates ?? []));
    }
}
