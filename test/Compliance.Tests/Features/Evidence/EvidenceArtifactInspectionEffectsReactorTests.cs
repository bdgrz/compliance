using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Evidence;

public sealed class EvidenceArtifactInspectionEffectsReactorTests
{
    const string Workload = "EvidenceArtifactInspectionEffectsV1";
    static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Collector = ActorReference.ForMember(Uuid.CreateVersion4(), "Collector");

    [Fact]
    public async Task ShouldRegisterIndependentTenantWorkloadGivenSharedComposition()
    {
        // Arrange
        await using var fixture = new Fixture();

        // Act
        var workload = Assert.Single(fixture.Workloads, item => item.Name == Workload);

        // Assert
        Assert.Equal(WorkloadScope.PerTenant, workload.Scope);
        var reactor = await fixture.ReactorAsync(fixture.TenantId);
        Assert.Equal(EventStreamPattern.ForPattern(fixture.TenantId.ToString(), "evidence-artifacts"), reactor.Pattern);
        Assert.Equal(Workload, reactor.Name);
    }

    [Theory]
    [InlineData(EvidenceInspectionOutcome.Malware)]
    [InlineData(EvidenceInspectionOutcome.SecretDetected)]
    [InlineData(EvidenceInspectionOutcome.Invalid)]
    public async Task ShouldFinishCanonicalEffectGivenCommittedVerdictWithoutCaptureRetry(EvidenceInspectionOutcome outcome)
    {
        // Arrange
        await using var fixture = new Fixture();
        var artifact = await fixture.SeedAsync(fixture.TenantId, "flagged"u8.ToArray(), outcome);
        var history = await fixture.HistoryAsync(artifact);
        fixture.Store.Calls.Clear();
        var writes = fixture.Store.Writes;

        // Act
        await fixture.RunAsync(fixture.TenantId);

        // Assert
        Assert.Equal(writes, fixture.Store.Writes);
        Assert.Equal(0, fixture.Inspector.Calls);
        Assert.All(fixture.Store.Calls, call => Assert.Equal(artifact.Content, call.Content));
        Assert.Equal(outcome == EvidenceInspectionOutcome.Malware ? "quarantine" : "delete",
            Assert.Single(fixture.Store.Calls).Operation);
        Assert.Null(await fixture.Local.OpenVerifiedAsync(artifact.Content));
        await using var quarantined = await fixture.Local.OpenQuarantinedAsync(artifact.Content);
        Assert.Equal(outcome == EvidenceInspectionOutcome.Malware, quarantined is not null);
        Assert.Equal(history, await fixture.HistoryAsync(artifact));
        var loaded = await fixture.LoadAsync(artifact);
        Assert.Equal(artifact.Inspection.State, loaded.State);
        Assert.Equal(artifact.Inspection.Reason, loaded.StateReason);
        Assert.Equal(artifact.Registration.Content, loaded.Content);
    }

    [Fact]
    public async Task ShouldUseCanonicalLengthGivenCaptureRetryReportsDifferentWrittenLength()
    {
        // Arrange
        await using var fixture = new Fixture();
        fixture.Inspector.State = ArtifactInspectionState.Quarantined;
        fixture.Store.RefuseQuarantine = true;
        var initial = await fixture.CaptureAsync("flagged"u8.ToArray());
        Assert.False(initial.IsSuccess);
        var canonical = fixture.Inspector.Last!;
        fixture.Store.RefuseQuarantine = false;
        fixture.Store.ReportedLengthDelta = 1;
        fixture.Store.Calls.Clear();

        // Act
        var recovered = await fixture.CaptureAsync("flagged"u8.ToArray());

        // Assert
        Assert.True(recovered.IsSuccess);
        Assert.Equal("quarantine", Assert.Single(fixture.Store.Calls).Operation);
        Assert.All(fixture.Store.Calls, call => Assert.Equal(canonical, call.Content));
        Assert.Equal(1, fixture.Inspector.Calls);
        Assert.Null(await fixture.Local.OpenVerifiedAsync(canonical));
        await using var quarantined = await fixture.Local.OpenQuarantinedAsync(canonical);
        Assert.NotNull(quarantined);
    }

    [Fact]
    public async Task ShouldPropagateCancellationGivenCaptureEffectCompletesAfterTokenCanceled()
    {
        // Arrange
        await using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Inspector.State = ArtifactInspectionState.Quarantined;
        fixture.Store.CancelAfterEffect = cancellation;

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.CaptureAsync("flagged"u8.ToArray(), cancellation.Token));

        // Assert
        var canonical = fixture.Inspector.Last!;
        await using var quarantined = await fixture.Local.OpenQuarantinedAsync(canonical);
        Assert.NotNull(quarantined);
        Assert.Equal(EvidenceArtifactStates.Quarantined,
            (await fixture.LoadAsync(new Seeded(canonical,
                EvidenceIntake.IdFor(canonical.TenantId, canonical.Sha256), null!, null!))).State);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("area")]
    [InlineData("artifact")]
    public async Task ShouldRejectSourceMismatchGivenValidApplicableTarget(string mismatch)
    {
        // Arrange
        await using var fixture = new Fixture();
        var target = await fixture.SeedAsync(fixture.TenantId, "flagged"u8.ToArray(), EvidenceInspectionOutcome.Malware);
        var source = new EventStreamAddress(
            mismatch == "tenant" ? Uuid.CreateVersion4().ToString() : fixture.TenantId.ToString(),
            mismatch == "area" ? "other-artifacts" : "evidence-artifacts",
            mismatch == "artifact" ? Uuid.CreateVersion4().ToString() : target.Id.ToString());
        var reactor = await fixture.ReactorAsync(fixture.TenantId);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Assert.IsAssignableFrom<IReactorHandler<EvidenceArtifactInspected>>(reactor)
                .HandleAsync(new Context(target.Inspection, source), CancellationToken.None).AsTask());

        // Assert
        Assert.Empty(fixture.Store.Calls);
        await using var available = await fixture.Local.OpenVerifiedAsync(target.Content);
        Assert.NotNull(available);
    }

    [Fact]
    public async Task ShouldRejectForeignBoundTenantGivenConsistentForeignSourceAndTrigger()
    {
        // Arrange
        await using var fixture = new Fixture();
        var foreign = await fixture.SeedAsync(Uuid.CreateVersion4(), "flagged"u8.ToArray(), EvidenceInspectionOutcome.Malware);
        var reactor = await fixture.ReactorAsync(fixture.TenantId);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Assert.IsAssignableFrom<IReactorHandler<EvidenceArtifactInspected>>(reactor)
                .HandleAsync(new Context(foreign.Inspection,
                    new EvidenceArtifact(foreign.Content.TenantId, foreign.Id).Stream), CancellationToken.None).AsTask());

        // Assert
        Assert.Empty(fixture.Store.Calls);
        await using var available = await fixture.Local.OpenVerifiedAsync(foreign.Content);
        Assert.NotNull(available);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("tenant")]
    [InlineData("artifact")]
    [InlineData("digest")]
    [InlineData("zero_length")]
    [InlineData("negative_length")]
    [InlineData("metadata")]
    public async Task ShouldFailWithoutContentOperationGivenMalformedCanonicalRegistration(string fault)
    {
        // Arrange
        await using var fixture = new Fixture();
        var target = await fixture.SeedMalformedAsync(fault);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync(fixture.TenantId));

        // Assert
        Assert.Empty(fixture.Store.Calls);
        await using var available = await fixture.Local.OpenVerifiedAsync(target.Content);
        Assert.NotNull(available);
        Assert.NotEqual(await fixture.CheckpointAfterAsync(fixture.TenantId, fault == "missing" ? 1 : 2),
            await fixture.Checkpoints.LoadAsync(Identity(fixture.TenantId)));
    }

    [Theory]
    [InlineData("quarantined", "invalid")]
    [InlineData("rejected", "malware")]
    [InlineData("rejected", "future_reason")]
    [InlineData("future_state", null)]
    public async Task ShouldFailWithoutContentOperationGivenUnrecognizedApplicableStateOrReason(string state, string? reason)
    {
        // Arrange
        await using var fixture = new Fixture();
        var target = await fixture.SeedAsync(fixture.TenantId, "flagged"u8.ToArray(), EvidenceInspectionOutcome.Malware);
        await fixture.AppendAsync(target, [new EvidenceArtifactInspected(fixture.TenantId,
            target.Id, state, reason, Now.AddMinutes(2))], 2);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync(fixture.TenantId));

        // Assert
        Assert.Empty(fixture.Store.Calls);
        await using var available = await fixture.Local.OpenVerifiedAsync(target.Content);
        Assert.NotNull(available);
    }

    [Fact]
    public async Task ShouldIsolateContentGivenIdenticalBytesAndVerdictsInTwoTenants()
    {
        // Arrange
        await using var fixture = new Fixture();
        var otherTenant = Uuid.CreateVersion4();
        var first = await fixture.SeedAsync(fixture.TenantId, "flagged"u8.ToArray(), EvidenceInspectionOutcome.Malware);
        var second = await fixture.SeedAsync(otherTenant, "flagged"u8.ToArray(), EvidenceInspectionOutcome.Malware);
        var otherHistory = await fixture.HistoryAsync(second);

        // Act
        await fixture.RunAsync(fixture.TenantId);

        // Assert
        Assert.Equal(first.Content.Sha256, second.Content.Sha256);
        Assert.All(fixture.Store.Calls, call => Assert.Equal(first.Content, call.Content));
        Assert.Null(await fixture.Local.OpenVerifiedAsync(first.Content));
        await using var available = await fixture.Local.OpenVerifiedAsync(second.Content);
        Assert.NotNull(available);
        Assert.Equal(otherHistory, await fixture.HistoryAsync(second));
        Assert.Equal(ProjectionCheckpoint.Start, await fixture.Checkpoints.LoadAsync(Identity(otherTenant)));
    }

    [Theory]
    [InlineData(EvidenceInspectionOutcome.Malware, false)]
    [InlineData(EvidenceInspectionOutcome.Malware, true)]
    [InlineData(EvidenceInspectionOutcome.SecretDetected, false)]
    [InlineData(EvidenceInspectionOutcome.SecretDetected, true)]
    [InlineData(EvidenceInspectionOutcome.Invalid, false)]
    [InlineData(EvidenceInspectionOutcome.Invalid, true)]
    public async Task ShouldResumeFailedVerdictGivenEarlierSavedProgressAndFreshReactor(
        EvidenceInspectionOutcome outcome, bool afterEffect)
    {
        // Arrange
        await using var fixture = new Fixture();
        var earlier = await fixture.SeedAsync(fixture.TenantId, "earlier"u8.ToArray(), EvidenceInspectionOutcome.Malware);
        var failed = await fixture.SeedAsync(fixture.TenantId, "flagged"u8.ToArray(), outcome);
        fixture.Store.FailingDigest = failed.Content.Sha256;
        fixture.Store.ThrowBeforeEffect = !afterEffect;
        fixture.Store.ThrowAfterEffect = afterEffect;
        var history = await fixture.HistoryAsync(failed);

        // Act
        await Assert.ThrowsAsync<IOException>(() => fixture.RunAsync(fixture.TenantId));
        var saved = await fixture.Checkpoints.LoadAsync(Identity(fixture.TenantId));
        fixture.Store.ThrowBeforeEffect = false;
        fixture.Store.ThrowAfterEffect = false;
        await fixture.RunAsync(fixture.TenantId);
        var count = fixture.Store.Calls.Count;
        await fixture.RunAsync(fixture.TenantId);

        // Assert
        Assert.Equal(await fixture.CheckpointAfterAsync(fixture.TenantId, 3), saved);
        Assert.Equal(await fixture.CheckpointAfterAsync(fixture.TenantId, 4),
            await fixture.Checkpoints.LoadAsync(Identity(fixture.TenantId)));
        Assert.Single(fixture.Store.Calls, call => call.Content == earlier.Content);
        Assert.Equal(count, fixture.Store.Calls.Count);
        Assert.Equal(history, await fixture.HistoryAsync(failed));
        Assert.Null(await fixture.Local.OpenVerifiedAsync(failed.Content));
        if (afterEffect && outcome == EvidenceInspectionOutcome.Malware)
        {
            Assert.Contains(fixture.Store.Calls, call => call.Operation == "open_quarantine" && call.Content == failed.Content);
            Assert.Equal(1, fixture.Store.VerificationDisposals);
        }
    }

    [Theory]
    [InlineData(EvidenceInspectionOutcome.Malware)]
    [InlineData(EvidenceInspectionOutcome.SecretDetected)]
    [InlineData(EvidenceInspectionOutcome.Invalid)]
    public async Task ShouldRecognizeAppliedEffectGivenCheckpointSaveFailureAndReplay(EvidenceInspectionOutcome outcome)
    {
        // Arrange
        await using var fixture = new Fixture();
        var target = await fixture.SeedAsync(fixture.TenantId, "flagged"u8.ToArray(), outcome);
        var completed = await fixture.CheckpointAfterAsync(fixture.TenantId, 2);
        fixture.Checkpoints.RefusedCursor = completed.Cursor.Value;

        // Act
        await Assert.ThrowsAsync<IOException>(() => fixture.RunAsync(fixture.TenantId));
        var saved = await fixture.Checkpoints.LoadAsync(Identity(fixture.TenantId));
        fixture.Checkpoints.RefusedCursor = null;
        await fixture.RunAsync(fixture.TenantId);

        // Assert
        Assert.Equal(await fixture.CheckpointAfterAsync(fixture.TenantId, 1), saved);
        Assert.Equal(completed, await fixture.Checkpoints.LoadAsync(Identity(fixture.TenantId)));
        Assert.Null(await fixture.Local.OpenVerifiedAsync(target.Content));
        Assert.Equal(outcome == EvidenceInspectionOutcome.Malware ? 1 : 0, fixture.Store.VerificationDisposals);
        if (outcome != EvidenceInspectionOutcome.Malware)
            Assert.Equal([ArtifactDeletionResult.Deleted, ArtifactDeletionResult.NotFound], fixture.Store.DeletionResults);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldKeepVerdictUncheckpointedGivenRefusedOrCorruptQuarantine(bool corrupt)
    {
        // Arrange
        await using var fixture = new Fixture();
        var target = await fixture.SeedAsync(fixture.TenantId, "flagged"u8.ToArray(), EvidenceInspectionOutcome.Malware);
        if (corrupt)
            await fixture.CorruptQuarantineAsync(target);
        else
            fixture.Store.RefuseQuarantine = true;

        // Act
        if (corrupt)
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.RunAsync(fixture.TenantId));
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync(fixture.TenantId));

        // Assert
        Assert.Equal(await fixture.CheckpointAfterAsync(fixture.TenantId, 1),
            await fixture.Checkpoints.LoadAsync(Identity(fixture.TenantId)));
        Assert.Equal(["quarantine", "open_quarantine"], fixture.Store.Calls.Select(call => call.Operation));
    }

    [Fact]
    public async Task ShouldRetainHoldGivenRejectedContentCannotBeDeleted()
    {
        // Arrange
        await using var fixture = new Fixture();
        var target = await fixture.SeedAsync(fixture.TenantId, "flagged"u8.ToArray(), EvidenceInspectionOutcome.SecretDetected);
        Assert.True(await fixture.Local.PlaceHoldAsync(target.Content, Uuid.CreateVersion4()));

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync(fixture.TenantId));

        // Assert
        Assert.Equal(await fixture.CheckpointAfterAsync(fixture.TenantId, 1),
            await fixture.Checkpoints.LoadAsync(Identity(fixture.TenantId)));
        Assert.Equal("delete", Assert.Single(fixture.Store.Calls).Operation);
        Assert.Equal(ArtifactDeletionResult.Held, await fixture.Local.DeleteAsync(target.Content));
        await using var available = await fixture.Local.OpenVerifiedAsync(target.Content);
        Assert.NotNull(available);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldAvoidCheckpointGivenCancellationBeforeOrAfterEffect(bool afterEffect)
    {
        // Arrange
        await using var fixture = new Fixture();
        var target = await fixture.SeedAsync(fixture.TenantId, "flagged"u8.ToArray(), EvidenceInspectionOutcome.Malware);
        using var cancellation = new CancellationTokenSource();
        if (afterEffect)
            fixture.Store.CancelAfterEffect = cancellation;
        else
            cancellation.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.RunAsync(fixture.TenantId, ct: cancellation.Token));

        // Assert
        var saved = await fixture.Checkpoints.LoadAsync(Identity(fixture.TenantId));
        Assert.Equal(afterEffect ? await fixture.CheckpointAfterAsync(fixture.TenantId, 1) : ProjectionCheckpoint.Start, saved);
        Assert.NotEqual(await fixture.CheckpointAfterAsync(fixture.TenantId, 2), saved);
        if (!afterEffect)
            Assert.Empty(fixture.Store.Calls);
        fixture.Store.CancelAfterEffect = null;
        await fixture.RunAsync(fixture.TenantId);
        Assert.Null(await fixture.Local.OpenVerifiedAsync(target.Content));
    }

    [Fact]
    public async Task ShouldPreserveHistoryGivenDuplicateDeliveryExplicitReplayAndCompletedPass()
    {
        // Arrange
        await using var fixture = new Fixture();
        var target = await fixture.SeedAsync(fixture.TenantId, "flagged"u8.ToArray(), EvidenceInspectionOutcome.Malware);
        var history = await fixture.HistoryAsync(target);

        // Act
        await fixture.RunAsync(fixture.TenantId);
        var count = fixture.Store.Calls.Count;
        await fixture.RunAsync(fixture.TenantId);
        Assert.Equal(count, fixture.Store.Calls.Count);
        await fixture.RunAsync(fixture.TenantId, start: ProjectionCheckpoint.Start);
        var reactor = await fixture.ReactorAsync(fixture.TenantId);
        await Assert.IsAssignableFrom<IReactorHandler<EvidenceArtifactInspected>>(reactor)
            .HandleAsync(new Context(target.Inspection, new EvidenceArtifact(fixture.TenantId, target.Id).Stream),
                CancellationToken.None);

        // Assert
        Assert.Equal(2, fixture.Store.VerificationDisposals);
        Assert.Equal(history, await fixture.HistoryAsync(target));
        Assert.Equal(0, fixture.Inspector.Calls);
        Assert.Null(await fixture.Local.OpenVerifiedAsync(target.Content));
    }

    [Fact]
    public async Task ShouldLeaveReleasedContentAvailableGivenOldMalwareTrigger()
    {
        // Arrange
        await using var fixture = new Fixture();
        var target = await fixture.SeedAsync(fixture.TenantId, "flagged"u8.ToArray(), EvidenceInspectionOutcome.Malware);
        await fixture.ReleaseAsync(target);
        var history = await fixture.HistoryAsync(target);

        // Act
        await fixture.RunAsync(fixture.TenantId);

        // Assert
        Assert.Empty(fixture.Store.Calls);
        Assert.Equal(0, fixture.Inspector.Calls);
        Assert.Equal(EvidenceArtifactStates.Available, (await fixture.LoadAsync(target)).State);
        Assert.Equal(history, await fixture.HistoryAsync(target));
        await using var available = await fixture.Local.OpenVerifiedAsync(target.Content);
        Assert.NotNull(available);
    }

    [Theory]
    [InlineData(EvidenceArtifactStates.PendingInspection)]
    [InlineData(EvidenceArtifactStates.Available)]
    [InlineData(EvidenceArtifactStates.Disposed)]
    public async Task ShouldPerformNoEffectGivenKnownNonapplicableCurrentState(string state)
    {
        // Arrange
        await using var fixture = new Fixture();
        var target = await fixture.SeedAsync(fixture.TenantId, "flagged"u8.ToArray(), EvidenceInspectionOutcome.Clean);
        await fixture.AppendAsync(target, [new EvidenceArtifactInspected(fixture.TenantId,
            target.Id, state, null, Now.AddMinutes(2))], 2);

        // Act
        await fixture.RunAsync(fixture.TenantId);

        // Assert
        Assert.Empty(fixture.Store.Calls);
        Assert.Equal(0, fixture.Inspector.Calls);
        await using var available = await fixture.Local.OpenVerifiedAsync(target.Content);
        Assert.NotNull(available);
    }

    sealed record Seeded(ArtifactContentReference Content, Uuid Id,
        EvidenceArtifactRegistered Registration, EvidenceArtifactInspected Inspection);

    sealed class Fixture : IAsyncDisposable
    {
        readonly string _root = Path.Combine(Path.GetTempPath(), $"bdgrz-artifact-effects-{Guid.NewGuid():N}");
        readonly ServiceProvider _provider;
        readonly List<AsyncServiceScope> _scopes = [];
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public InMemoryEventStore Events { get; } = new();
        public CheckpointStore Checkpoints { get; } = new();
        public LocalArtifactContentStore Local { get; }
        public EffectStore Store { get; }
        public Inspector Inspector { get; } = new();
        public IReadOnlyList<WorkloadRegistration> Workloads { get; }

        public Fixture()
        {
            Local = new LocalArtifactContentStore(new ArtifactContentStoreOptions(_root, new byte[32], 1024),
                TimeProvider.System);
            Store = new EffectStore(Local);
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            }).Build();
            _ = services.AddCompliance(configuration);
            services.AddSingleton<IEventStore>(Events);
            services.AddSingleton<IProjectionCheckpointStore>(Checkpoints);
            services.AddSingleton<IArtifactContentStore>(Store);
            services.AddSingleton<IArtifactInspector>(Inspector);
            Workloads = services.Where(descriptor => descriptor.ServiceType == typeof(WorkloadRegistration))
                .Select(descriptor => (WorkloadRegistration)descriptor.ImplementationInstance!).ToArray();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        public async Task<Reactor> ReactorAsync(Uuid tenantId)
        {
            var workload = Assert.Single(Workloads, item => item.Name == Workload);
            var scope = _provider.CreateAsyncScope();
            _scopes.Add(scope);
            var reactor = Assert.IsAssignableFrom<Reactor>(scope.ServiceProvider.GetRequiredService(workload.ComponentType));
            // Bind an empty scenario, then use the durable fixture source and saved checkpoint below.
            await new ReactorScenario(new TenantId(tenantId.ToString())).RunAsync(reactor);
            return reactor;
        }

        public async Task<ProjectionCheckpoint> RunAsync(Uuid tenantId, ProjectionCheckpoint? start = null,
            CancellationToken ct = default)
        {
            var reactor = await ReactorAsync(tenantId);
            var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);
            return await new ReactorRunner(Events, _provider.GetRequiredService<IReactorPrincipalProvider>())
                .RunAsync(reactor, start ?? await Checkpoints.LoadAsync(identity, ct), ct: ct);
        }

        public async Task<Seeded> SeedAsync(Uuid tenantId, byte[] bytes, EvidenceInspectionOutcome outcome)
        {
            var written = await Local.StoreIfAbsentAsync(tenantId, new MemoryStream(bytes));
            var id = EvidenceIntake.IdFor(tenantId, written.Content.Sha256);
            await using var scope = _provider.CreateAsyncScope();
            var artifact = new EvidenceArtifact(tenantId, id);
            Assert.True(artifact.Register(Metadata(), written.Content.Sha256, written.Content.Length, Collector, Now).IsSuccess);
            Assert.Null(artifact.RecordInspection(outcome, Now.AddMinutes(1)));
            await scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(artifact, Dispatch());
            var history = await HistoryAsync(new Seeded(written.Content, id, null!, null!));
            return new Seeded(written.Content, id, Assert.IsType<EvidenceArtifactRegistered>(history[0]),
                Assert.IsType<EvidenceArtifactInspected>(history[1]));
        }

        public async Task<EvidenceArtifact> LoadAsync(Seeded artifact)
        {
            await using var scope = _provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
                .HydrateAsync(new EvidenceArtifact(artifact.Content.TenantId, artifact.Id));
        }

        public async Task<Seeded> SeedMalformedAsync(string fault)
        {
            var written = await Local.StoreIfAbsentAsync(TenantId, new MemoryStream("flagged"u8.ToArray()));
            var id = EvidenceIntake.IdFor(TenantId, written.Content.Sha256);
            var registration = new EvidenceArtifactRegistered(
                fault == "tenant" ? Uuid.CreateVersion4() : TenantId,
                fault == "artifact" ? Uuid.CreateVersion4() : id,
                fault == "metadata" ? null! : Metadata(),
                fault == "digest" ? "INVALID" : written.Content.Sha256,
                fault == "zero_length" ? 0 : fault == "negative_length" ? -1 : written.Content.Length, Collector, Now);
            var inspection = new EvidenceArtifactInspected(TenantId, id, EvidenceArtifactStates.Quarantined,
                EvidenceArtifactStates.Malware, Now.AddMinutes(1));
            var target = new Seeded(written.Content, id, registration, inspection);
            await AppendAsync(target, fault == "missing" ? [inspection] : [registration, inspection], 0);
            return target;
        }

        public async Task AppendAsync(Seeded target, IReadOnlyList<DomainEvent> events, ulong offset)
        {
            for (var index = 0; index < events.Count; index++)
                events[index].AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), target.Id,
                    checked(offset + (ulong)index + 1), Now.AddMinutes(index + 2)));
            await Events.AppendAsync(new EvidenceArtifact(target.Content.TenantId, target.Id).Stream, offset, events);
        }

        public async Task<ProjectionCheckpoint> CheckpointAfterAsync(Uuid tenantId, int count)
        {
            var seen = 0;
            await foreach (var record in Events.ReadAsync(Identity(tenantId).Pattern, EventCursor.Start, CancellationToken.None))
                if (++seen == count)
                    return new ProjectionCheckpoint(record.NextCursor);
            throw new InvalidOperationException("Expected durable progress was not present.");
        }

        public async Task ReleaseAsync(Seeded target)
        {
            Assert.True(await Local.QuarantineAsync(target.Content));
            Assert.True(await Local.ReleaseQuarantineAsync(target.Content));
            await using var scope = _provider.CreateAsyncScope();
            var artifact = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
                .HydrateAsync(new EvidenceArtifact(target.Content.TenantId, target.Id));
            Assert.Null(artifact.ReleaseQuarantine("False positive cleared.", Collector, Now.AddMinutes(2)));
            await scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(artifact, Dispatch());
        }

        public async Task CorruptQuarantineAsync(Seeded target)
        {
            Assert.True(await Local.QuarantineAsync(target.Content));
            var path = Path.Combine(_root, target.Content.TenantId.ToString(), "quarantine", "sha256",
                target.Content.Sha256[..2], target.Content.Sha256);
            await File.WriteAllBytesAsync(path, new byte[checked((int)target.Content.Length)]);
        }

        public async Task<DomainEvent[]> HistoryAsync(Seeded artifact)
        {
            var events = new List<DomainEvent>();
            await foreach (var record in Events.ReadAsync(new EvidenceArtifact(artifact.Content.TenantId, artifact.Id).Stream))
                events.Add(record.Event);
            return events.ToArray();
        }

        public async Task<Result<EvidenceCapture>> CaptureAsync(byte[] bytes, CancellationToken ct = default)
        {
            await using var scope = _provider.CreateAsyncScope();
            var intake = new EvidenceIntake(Store, Inspector,
                scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
                scope.ServiceProvider.GetRequiredService<IAggregateWriter>(), TimeProvider.System);
            return await intake.CaptureAsync(TenantId, new MemoryStream(bytes), Metadata(), Collector, Dispatch(), ct);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var scope in _scopes)
                await scope.DisposeAsync();
            await _provider.DisposeAsync();
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }

    sealed class CheckpointStore : IProjectionCheckpointStore
    {
        readonly InMemoryProjectionCheckpointStore _inner = new();
        public string? RefusedCursor { get; set; }
        public ValueTask<ProjectionCheckpoint> LoadAsync(CheckpointIdentity identity, CancellationToken ct = default) =>
            _inner.LoadAsync(identity, ct);
        public ValueTask SaveAsync(CheckpointIdentity identity, ProjectionCheckpoint checkpoint, CancellationToken ct = default)
        {
            if (checkpoint.Cursor.Value == RefusedCursor)
                throw new IOException("Checkpoint acknowledgement failed.");
            return _inner.SaveAsync(identity, checkpoint, ct);
        }
    }

    sealed class Inspector : IArtifactInspector
    {
        public int Calls { get; private set; }
        public ArtifactInspectionState State { get; set; } = ArtifactInspectionState.NotInspected;
        public ArtifactContentReference? Last { get; private set; }
        public ValueTask<ArtifactInspectionResult> InspectAsync(ArtifactContentReference content, CancellationToken ct = default)
        {
            Calls++;
            Last = content;
            return ValueTask.FromResult(new ArtifactInspectionResult(State));
        }
    }

    sealed class EffectStore(IArtifactContentStore inner) : IArtifactContentStore
    {
        public List<(string Operation, ArtifactContentReference Content)> Calls { get; } = [];
        public int Writes { get; private set; }
        public bool RefuseQuarantine { get; set; }
        public bool RefuseDeletion { get; set; }
        public bool ThrowBeforeEffect { get; set; }
        public bool ThrowAfterEffect { get; set; }
        public string? FailingDigest { get; set; }
        public long ReportedLengthDelta { get; set; }
        public CancellationTokenSource? CancelAfterEffect { get; set; }
        public int VerificationDisposals { get; private set; }
        public List<ArtifactDeletionResult> DeletionResults { get; } = [];
        public async ValueTask<ArtifactContentWrite> StoreIfAbsentAsync(Uuid tenantId, Stream content, CancellationToken ct = default)
        {
            Writes++;
            var written = await inner.StoreIfAbsentAsync(tenantId, content, ct);
            return written with { Content = written.Content with { Length = written.Content.Length + ReportedLengthDelta } };
        }
        public ValueTask<Stream?> OpenVerifiedAsync(ArtifactContentReference content, CancellationToken ct = default) =>
            inner.OpenVerifiedAsync(content, ct);
        public ValueTask<ArtifactDelivery?> IssueDeliveryAsync(ArtifactContentReference content, TimeSpan lifetime,
            CancellationToken ct = default) => inner.IssueDeliveryAsync(content, lifetime, ct);
        public ValueTask<bool> PlaceHoldAsync(ArtifactContentReference content, Uuid holdId, CancellationToken ct = default) =>
            inner.PlaceHoldAsync(content, holdId, ct);
        public ValueTask<bool> RemoveHoldAsync(ArtifactContentReference content, Uuid holdId, CancellationToken ct = default) =>
            inner.RemoveHoldAsync(content, holdId, ct);
        public async ValueTask<bool> QuarantineAsync(ArtifactContentReference content, CancellationToken ct = default)
        {
            Calls.Add(("quarantine", content));
            Before(content);
            var result = !RefuseQuarantine && await inner.QuarantineAsync(content, ct);
            After(content);
            return result;
        }
        public ValueTask<bool> ReleaseQuarantineAsync(ArtifactContentReference content, CancellationToken ct = default) =>
            inner.ReleaseQuarantineAsync(content, ct);
        public async ValueTask<Stream?> OpenQuarantinedAsync(ArtifactContentReference content, CancellationToken ct = default)
        {
            Calls.Add(("open_quarantine", content));
            var stream = await inner.OpenQuarantinedAsync(content, ct);
            return stream is null ? null : new VerificationStream(stream, () => VerificationDisposals++);
        }
        public async ValueTask<ArtifactDeletionResult> DeleteAsync(ArtifactContentReference content, CancellationToken ct = default)
        {
            Calls.Add(("delete", content));
            Before(content);
            var result = RefuseDeletion ? ArtifactDeletionResult.Held : await inner.DeleteAsync(content, ct);
            DeletionResults.Add(result);
            After(content);
            return result;
        }
        void Before(ArtifactContentReference content)
        {
            if (ThrowBeforeEffect && (FailingDigest is null || FailingDigest == content.Sha256))
                throw new IOException("Storage failed before its effect.");
        }
        void After(ArtifactContentReference content)
        {
            CancelAfterEffect?.Cancel();
            if (ThrowAfterEffect && (FailingDigest is null || FailingDigest == content.Sha256))
                throw new IOException("Storage failed after its effect.");
        }
    }

    sealed class VerificationStream(Stream inner, Action disposed) : Stream
    {
        bool _disposed;
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                disposed();
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                disposed();
                await inner.DisposeAsync();
            }
            await base.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }

    sealed class Context(EvidenceArtifactInspected trigger, EventStreamAddress stream)
        : IReactorContext<EvidenceArtifactInspected>
    {
        public EvidenceArtifactInspected Trigger { get; } = trigger;
        public DomainEventRecord Source { get; } = new(stream, trigger, 0, EventCursor.Start);
        public ClaimsPrincipal Actor { get; } = RequestActor.CreateSystem("reactor:" + Workload, "bdgrz.system");
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid CauseId { get; } = Uuid.CreateVersion4();
        public DateTimeOffset StartedAt { get; } = Now;
    }

    static CheckpointIdentity Identity(Uuid tenantId) => new(Workload,
        EventStreamPattern.ForPattern(tenantId.ToString(), "evidence-artifacts"));

    static RequestDispatchContext Dispatch() => new(RequestActor.System);
    static EvidenceArtifactContent Metadata() => new("Quarterly export", "Original capture context.", "system_export",
        "Manual upload", Now.AddHours(-1), new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30), "confidential");
}
