using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Evidence;

public sealed class EvidenceIntakeTests : IDisposable
{
    static readonly ActorReference Collector = ActorReference.ForMember(Uuid.CreateVersion4(), "Collector");
    readonly string _root = Path.Combine(Path.GetTempPath(), $"bdgrz-evidence-{Guid.NewGuid():N}");
    readonly ServiceProvider _provider;
    readonly AsyncServiceScope _scope;
    readonly LocalArtifactContentStore _store;
    readonly Inspector _inspector = new();
    readonly InterruptedEventStore _events = new(new InMemoryEventStore());
    readonly Uuid _tenantId = Uuid.CreateVersion4();

    public EvidenceIntakeTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(_events);
        services.AddSingleton(TimeProvider.System);
        services.AddPortia();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
        _store = new LocalArtifactContentStore(new ArtifactContentStoreOptions(_root,
            new byte[32], 64), TimeProvider.System);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task ShouldKeepDurablePendingInspectionGivenInspectorFailure()
    {
        // Arrange
        _inspector.Failure = new IOException("The scanner is unavailable.");

        // Act
        await Assert.ThrowsAsync<IOException>(() => CaptureAsync("export"u8.ToArray()));

        // Assert
        var pending = await LoadAsync(EvidenceIntake.IdFor(_tenantId, _inspector.Last!.Sha256));
        Assert.True(pending.IsCreated);
        Assert.Equal(EvidenceArtifactStates.PendingInspection, pending.State);
        Assert.Equal("Quarterly export", pending.Content!.Title);
    }

    [Fact]
    public async Task ShouldResumeInspectionAndPreserveOriginalRegistrationGivenPendingRetry()
    {
        // Arrange
        var first = await CaptureAsync("export"u8.ToArray());
        var registered = await RegistrationAsync(first.Value.ArtifactId);
        _inspector.State = ArtifactInspectionState.Clean;
        var retryCollector = ActorReference.ForMember(Uuid.CreateVersion4(), "Retry collector");

        // Act
        var retried = await Intake().CaptureAsync(_tenantId, new MemoryStream("export"u8.ToArray()),
            Metadata("Replacement title"), retryCollector, Dispatch(), CancellationToken.None);

        // Assert
        Assert.True(retried.IsSuccess);
        Assert.True(retried.Value.Duplicate);
        Assert.Equal(first.Value.ArtifactId, retried.Value.ArtifactId);
        var completed = await LoadAsync(retried.Value.ArtifactId);
        Assert.Equal(EvidenceArtifactStates.Available, completed.State);
        Assert.Equal("Quarterly export", completed.Content!.Title);
        Assert.Equal(registered, await RegistrationAsync(retried.Value.ArtifactId));
    }

    [Fact]
    public async Task ShouldPreserveVerdictAndRecoverGivenQuarantineWasNotApplied()
    {
        // Arrange
        _inspector.State = ArtifactInspectionState.Quarantined;
        var store = new EffectStore(_store) { RefuseQuarantine = true };

        // Act
        var failed = await CaptureAsync("infected"u8.ToArray(), store: store);

        // Assert
        Assert.False(failed.IsSuccess, "A failed quarantine incorrectly returned successful capture.");
        Assert.Equal(RequestErrorKind.Conflict, failed.Error!.Kind);
        var id = EvidenceIntake.IdFor(_tenantId, _inspector.Last!.Sha256);
        Assert.Equal(EvidenceArtifactStates.Quarantined, (await LoadAsync(id)).State);
        store.RefuseQuarantine = false;
        var recovered = await CaptureAsync("infected"u8.ToArray(), store: store);
        Assert.True(recovered.Value.Duplicate);
        Assert.Equal(EvidenceArtifactStates.Quarantined, recovered.Value.State);
        Assert.Null(await _store.OpenVerifiedAsync(_inspector.Last!));
    }

    [Theory]
    [InlineData(ArtifactInspectionState.SecretDetected)]
    [InlineData(ArtifactInspectionState.Invalid)]
    public async Task ShouldPreserveVerdictAndRecoverGivenRejectedContentCannotBePurged(
        ArtifactInspectionState state)
    {
        // Arrange
        _inspector.State = state;
        var store = new EffectStore(_store) { RefuseDeletion = true };

        // Act
        var failed = await CaptureAsync("rejected"u8.ToArray(), store: store);

        // Assert
        Assert.False(failed.IsSuccess, "A refused purge incorrectly returned successful capture.");
        Assert.Equal(RequestErrorKind.Conflict, failed.Error!.Kind);
        var id = EvidenceIntake.IdFor(_tenantId, _inspector.Last!.Sha256);
        Assert.Equal(EvidenceArtifactStates.Rejected, (await LoadAsync(id)).State);
        store.RefuseDeletion = false;
        var recovered = await CaptureAsync("rejected"u8.ToArray(), store: store);
        Assert.True(recovered.Value.Duplicate);
        Assert.Equal(EvidenceArtifactStates.Rejected, recovered.Value.State);
        Assert.Null(await _store.OpenVerifiedAsync(_inspector.Last!));
        Assert.Null(await _store.OpenQuarantinedAsync(_inspector.Last!));
    }

    [Fact]
    public async Task ShouldResumeRecordedVerdictGivenInterruptedQuarantineStorage()
    {
        // Arrange
        _inspector.State = ArtifactInspectionState.Quarantined;
        _inspector.ContentStore = _store;
        var store = new EffectStore(_store) { ThrowAfterQuarantine = true };

        // Act
        await Assert.ThrowsAsync<IOException>(() => CaptureAsync("infected"u8.ToArray(), store: store));
        store.ThrowAfterQuarantine = false;
        var recovered = await CaptureAsync("infected"u8.ToArray(), store: store);

        // Assert
        Assert.True(recovered.Value.Duplicate);
        Assert.Equal(EvidenceArtifactStates.Quarantined, recovered.Value.State);
        Assert.Null(await _store.OpenVerifiedAsync(_inspector.Last!));
        await using var quarantined = await _store.OpenQuarantinedAsync(_inspector.Last!);
        Assert.NotNull(quarantined);
    }

    [Theory]
    [InlineData(ArtifactInspectionState.SecretDetected)]
    [InlineData(ArtifactInspectionState.Invalid)]
    public async Task ShouldPreserveRejectedVerdictGivenInterruptedPurge(ArtifactInspectionState state)
    {
        // Arrange
        _inspector.State = state;
        _inspector.ContentStore = _store;
        var store = new EffectStore(_store) { ThrowAfterDeletion = true };

        // Act
        await Assert.ThrowsAsync<IOException>(() => CaptureAsync("rejected"u8.ToArray(), store: store));
        store.ThrowAfterDeletion = false;
        _inspector.State = ArtifactInspectionState.Clean;
        var recovered = await CaptureAsync("rejected"u8.ToArray(), store: store);

        // Assert
        Assert.True(recovered.Value.Duplicate);
        Assert.Equal(EvidenceArtifactStates.Rejected, recovered.Value.State);
        Assert.Equal(state == ArtifactInspectionState.SecretDetected
            ? EvidenceArtifactStates.SecretDetected
            : EvidenceArtifactStates.Invalid, recovered.Value.Reason);
        Assert.Null(await _store.OpenVerifiedAsync(_inspector.Last!));
        Assert.Null(await _store.OpenQuarantinedAsync(_inspector.Last!));
    }

    [Fact]
    public async Task ShouldResumeInspectionGivenScannerFailureOnFirstAttempt()
    {
        // Arrange
        _inspector.Failure = new IOException("The scanner is unavailable.");
        await Assert.ThrowsAsync<IOException>(() => CaptureAsync("export"u8.ToArray()));
        var id = EvidenceIntake.IdFor(_tenantId, _inspector.Last!.Sha256);
        var original = await RegistrationAsync(id);
        _inspector.Failure = null;
        _inspector.State = ArtifactInspectionState.Clean;

        // Act
        var recovered = await CaptureAsync("export"u8.ToArray(), "Retry title");

        // Assert
        Assert.True(recovered.Value.Duplicate);
        Assert.Equal(id, recovered.Value.ArtifactId);
        Assert.Equal(EvidenceArtifactStates.Available, recovered.Value.State);
        Assert.Equal(original, await RegistrationAsync(id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRecoverWithoutApplyingQuarantineGivenEventAppendFailure(bool failRegistration)
    {
        // Arrange
        var written = await _store.StoreIfAbsentAsync(_tenantId, new MemoryStream("infected"u8.ToArray()));
        var id = EvidenceIntake.IdFor(_tenantId, written.Content.Sha256);
        _inspector.State = ArtifactInspectionState.Quarantined;
        _inspector.ContentStore = _store;
        _events.FailRegistration = failRegistration;
        _events.FailInspection = !failRegistration;

        // Act
        await Assert.ThrowsAsync<IOException>(() => CaptureAsync("infected"u8.ToArray()));

        // Assert
        var checkpoint = await LoadAsync(id);
        Assert.Equal(!failRegistration, checkpoint.IsCreated);
        if (checkpoint.IsCreated)
            Assert.Equal(EvidenceArtifactStates.PendingInspection, checkpoint.State);
        await using var available = await _store.OpenVerifiedAsync(written.Content);
        Assert.NotNull(available);
        Assert.Null(await _store.OpenQuarantinedAsync(written.Content));
        _events.FailRegistration = false;
        _events.FailInspection = false;
        var recovered = await CaptureAsync("infected"u8.ToArray());
        Assert.Equal(EvidenceArtifactStates.Quarantined, recovered.Value.State);
        Assert.Equal(!failRegistration, recovered.Value.Duplicate);
        Assert.Null(await _store.OpenVerifiedAsync(written.Content));
    }

    [Theory]
    [InlineData(ArtifactInspectionState.Quarantined, "quarantined")]
    [InlineData(ArtifactInspectionState.SecretDetected, "rejected")]
    [InlineData(ArtifactInspectionState.Invalid, "rejected")]
    public async Task ShouldReplayRecordedVerdictGivenStorageFailureBeforeEffect(
        ArtifactInspectionState inspection, string expectedState)
    {
        // Arrange
        _inspector.State = inspection;
        var store = new EffectStore(_store) { ThrowBeforeEffect = true };

        // Act
        await Assert.ThrowsAsync<IOException>(() => CaptureAsync("flagged"u8.ToArray(), store: store));

        // Assert
        var id = EvidenceIntake.IdFor(_tenantId, _inspector.Last!.Sha256);
        Assert.Equal(expectedState, (await LoadAsync(id)).State);
        var registered = await RegistrationAsync(id);
        store.ThrowBeforeEffect = false;
        _inspector.Failure = new IOException("A recorded verdict must not be rescanned.");
        var recovered = await CaptureAsync("flagged"u8.ToArray(), store: store);
        Assert.Equal(expectedState, recovered.Value.State);
        Assert.True(recovered.Value.Duplicate);
        Assert.Equal(registered, await RegistrationAsync(id));
        Assert.Null(await _store.OpenVerifiedAsync(_inspector.Last!));
    }

    [Fact]
    public async Task ShouldRefuseSuccessfulRetryGivenRejectedContentCannotBePurgedAgain()
    {
        // Arrange
        _inspector.State = ArtifactInspectionState.SecretDetected;
        var original = await CaptureAsync("secret"u8.ToArray());
        var store = new EffectStore(_store) { RefuseDeletion = true };
        _inspector.Failure = new IOException("A rejected artifact must not be rescanned.");

        // Act
        var retried = await CaptureAsync("secret"u8.ToArray(), store: store);

        // Assert
        Assert.False(retried.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, retried.Error!.Kind);
        Assert.Equal(EvidenceArtifactStates.Rejected, (await LoadAsync(original.Value.ArtifactId)).State);
        store.RefuseDeletion = false;
        var recovered = await CaptureAsync("secret"u8.ToArray(), store: store);
        Assert.Equal(EvidenceArtifactStates.Rejected, recovered.Value.State);
        Assert.Null(await _store.OpenVerifiedAsync(_inspector.Last!));
    }

    [Fact]
    public async Task ShouldUseCurrentAvailableStateGivenReleaseCommittedBeforeStorageDecision()
    {
        // Arrange
        _inspector.State = ArtifactInspectionState.Quarantined;
        var reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = new ReleaseBeforeReconciliationWriter(
            _scope.ServiceProvider.GetRequiredService<IAggregateWriter>(), reader, _store, _tenantId);
        var intake = new EvidenceIntake(_store, _inspector, reader, writer, TimeProvider.System);

        // Act
        var captured = await intake.CaptureAsync(_tenantId, new MemoryStream("infected"u8.ToArray()),
            Metadata("Quarterly export"), Collector, Dispatch(), CancellationToken.None);

        // Assert
        Assert.True(captured.IsSuccess);
        Assert.Equal(EvidenceArtifactStates.Available, captured.Value.State);
        Assert.Equal(EvidenceArtifactStates.Available, (await LoadAsync(captured.Value.ArtifactId)).State);
        Assert.Null(captured.Value.Reason);
        await using var available = await _store.OpenVerifiedAsync(_inspector.Last!);
        Assert.NotNull(available);
        Assert.Null(await _store.OpenQuarantinedAsync(_inspector.Last!));
    }

    [Fact]
    public async Task ShouldPreserveWinningVerdictGivenConcurrentPendingInspection()
    {
        // Arrange
        var pending = await CaptureAsync("export"u8.ToArray());
        var original = await RegistrationAsync(pending.Value.ArtifactId);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _inspector.InspectAction = async (_, ct) =>
        {
            entered.SetResult();
            await resume.Task.WaitAsync(ct);
            return new ArtifactInspectionResult(ArtifactInspectionState.SecretDetected);
        };
        var competing = new EvidenceIntake(_store, new Inspector { State = ArtifactInspectionState.Clean },
            _scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            _scope.ServiceProvider.GetRequiredService<IAggregateWriter>(), TimeProvider.System);

        // Act
        var stale = CaptureAsync("export"u8.ToArray());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Result<EvidenceCapture> winner;
        try
        {
            winner = await competing.CaptureAsync(_tenantId, new MemoryStream("export"u8.ToArray()),
                Metadata("Competing title"), Collector, Dispatch(), CancellationToken.None);
        }
        finally
        {
            resume.SetResult();
        }
        await Assert.ThrowsAsync<EventStreamConcurrencyException>(() => stale);

        // Assert
        Assert.Equal(EvidenceArtifactStates.Available, winner.Value.State);
        Assert.Equal(EvidenceArtifactStates.Available, (await LoadAsync(pending.Value.ArtifactId)).State);
        Assert.Equal(original, await RegistrationAsync(pending.Value.ArtifactId));
        await using var available = await _store.OpenVerifiedAsync(_inspector.Last!);
        Assert.NotNull(available);
    }

    [Fact]
    public async Task ShouldStayPendingInspectionGivenNoInspectionEngine()
    {
        // Arrange
        _inspector.State = ArtifactInspectionState.NotInspected;

        // Act
        var result = await CaptureAsync("export"u8.ToArray());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Duplicate);
        Assert.Equal(EvidenceArtifactStates.PendingInspection, result.Value.State);
        Assert.Equal(EvidenceArtifactStates.PendingInspection, (await LoadAsync(result.Value.ArtifactId)).State);
    }

    [Fact]
    public async Task ShouldBecomeAvailableGivenCleanInspection()
    {
        // Arrange
        _inspector.State = ArtifactInspectionState.Clean;

        // Act
        var result = await CaptureAsync("export"u8.ToArray());

        // Assert
        Assert.Equal(EvidenceArtifactStates.Available, result.Value.State);
        Assert.Equal(EvidenceArtifactStates.Available, (await LoadAsync(result.Value.ArtifactId)).State);
    }

    [Fact]
    public async Task ShouldWithholdContentGivenMalwareInspection()
    {
        // Arrange
        _inspector.State = ArtifactInspectionState.Quarantined;

        // Act
        var result = await CaptureAsync("infected"u8.ToArray());

        // Assert
        Assert.Equal(EvidenceArtifactStates.Quarantined, result.Value.State);
        Assert.Equal(EvidenceArtifactStates.Malware, result.Value.Reason);
        Assert.Null(await _store.OpenVerifiedAsync(_inspector.Last!));
        await using var held = await _store.OpenQuarantinedAsync(_inspector.Last!);
        Assert.NotNull(held);
    }

    [Fact]
    public async Task ShouldPurgeContentAndKeepTombstoneGivenSecretDetected()
    {
        // Arrange
        _inspector.State = ArtifactInspectionState.SecretDetected;

        // Act
        var result = await CaptureAsync("AKIA-secret"u8.ToArray());

        // Assert
        Assert.Equal(EvidenceArtifactStates.Rejected, result.Value.State);
        Assert.Equal(EvidenceArtifactStates.SecretDetected, result.Value.Reason);
        Assert.Null(await _store.OpenVerifiedAsync(_inspector.Last!));
        Assert.Null(await _store.OpenQuarantinedAsync(_inspector.Last!));
        var tombstone = await LoadAsync(result.Value.ArtifactId);
        Assert.Equal(_inspector.Last!.Sha256, tombstone.ContentSha256);
    }

    [Fact]
    public async Task ShouldPurgeAgainAndReportDuplicateGivenSecretReuploaded()
    {
        // Arrange
        _inspector.State = ArtifactInspectionState.SecretDetected;
        var first = await CaptureAsync("AKIA-secret"u8.ToArray());

        // Act
        var second = await CaptureAsync("AKIA-secret"u8.ToArray());

        // Assert
        Assert.True(second.Value.Duplicate);
        Assert.Equal(first.Value.ArtifactId, second.Value.ArtifactId);
        Assert.Equal(EvidenceArtifactStates.Rejected, second.Value.State);
        Assert.Null(await _store.OpenVerifiedAsync(_inspector.Last!));
    }

    [Fact]
    public async Task ShouldReturnExistingIdentityAsDuplicateGivenSameContentInTenant()
    {
        // Arrange
        _inspector.State = ArtifactInspectionState.Clean;
        var first = await CaptureAsync("export"u8.ToArray());

        // Act
        var second = await CaptureAsync("export"u8.ToArray(), title: "Another title");

        // Assert
        Assert.False(first.Value.Duplicate);
        Assert.True(second.Value.Duplicate);
        Assert.Equal(first.Value.ArtifactId, second.Value.ArtifactId);
        Assert.Equal("Quarterly export", (await LoadAsync(first.Value.ArtifactId)).Content!.Title);
    }

    [Fact]
    public async Task ShouldKeepSeparateIdentitiesGivenSameContentInAnotherTenant()
    {
        // Arrange
        _inspector.State = ArtifactInspectionState.Clean;
        var first = await CaptureAsync("export"u8.ToArray());

        // Act
        var other = await Intake().CaptureAsync(Uuid.CreateVersion4(),
            new MemoryStream("export"u8.ToArray()), Metadata("Quarterly export"), Collector, Dispatch(),
            CancellationToken.None);

        // Assert
        Assert.False(other.Value.Duplicate);
        Assert.NotEqual(first.Value.ArtifactId, other.Value.ArtifactId);
    }

    [Fact]
    public async Task ShouldRejectWithoutStoringGivenInvalidMetadata()
    {
        // Arrange
        var bytes = "export"u8.ToArray();

        // Act
        var result = await CaptureAsync(bytes, title: " ");

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.False(Directory.Exists(_root) && Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public async Task ShouldRejectWithoutRegisteringGivenOversizedContent()
    {
        // Arrange
        var bytes = new byte[65];

        // Act
        var result = await CaptureAsync(bytes);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Null(_inspector.Last);
    }

    Task<Result<EvidenceCapture>> CaptureAsync(byte[] bytes, string title = "Quarterly export",
        IArtifactContentStore? store = null) =>
        Intake(store).CaptureAsync(_tenantId, new MemoryStream(bytes), Metadata(title), Collector, Dispatch(),
            CancellationToken.None).AsTask();

    EvidenceIntake Intake(IArtifactContentStore? store = null) => new(store ?? _store, _inspector,
        _scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
        _scope.ServiceProvider.GetRequiredService<IAggregateWriter>(), TimeProvider.System);

    async Task<EvidenceArtifact> LoadAsync(Uuid artifactId) =>
        await _scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new EvidenceArtifact(_tenantId, artifactId));

    async Task<EvidenceArtifactRegistered> RegistrationAsync(Uuid artifactId)
    {
        await foreach (var record in _scope.ServiceProvider.GetRequiredService<IEventStore>()
                           .ReadAsync(new EvidenceArtifact(_tenantId, artifactId).Stream))
            return Assert.IsType<EvidenceArtifactRegistered>(record.Event);
        throw new InvalidOperationException("The evidence registration was not persisted.");
    }

    static RequestDispatchContext Dispatch() => new(RequestActor.System);

    static EvidenceArtifactContent Metadata(string title) =>
        new(title, null, "system_export", "Okta", DateTimeOffset.UtcNow.AddHours(-1),
            new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30), "confidential");

    sealed class Inspector : IArtifactInspector
    {
        public ArtifactInspectionState State { get; set; } = ArtifactInspectionState.NotInspected;
        public Exception? Failure { get; set; }
        public IArtifactContentStore? ContentStore { get; set; }
        public Func<ArtifactContentReference, CancellationToken, ValueTask<ArtifactInspectionResult>>? InspectAction
        { get; set; }
        public ArtifactContentReference? Last { get; private set; }

        public async ValueTask<ArtifactInspectionResult> InspectAsync(ArtifactContentReference content,
            CancellationToken ct = default)
        {
            Last = content;
            if (InspectAction is { } action)
                return await action(content, ct);
            if (Failure is { } failure)
                throw failure;
            if (ContentStore is { } store)
            {
                await using var opened = await store.OpenVerifiedAsync(content, ct);
                if (opened is null)
                    throw new IOException("The content is not available for inspection.");
            }
            return new ArtifactInspectionResult(State);
        }
    }

    sealed class EffectStore(IArtifactContentStore inner) : IArtifactContentStore
    {
        public bool RefuseQuarantine { get; set; }
        public bool RefuseDeletion { get; set; }
        public bool ThrowAfterQuarantine { get; set; }
        public bool ThrowAfterDeletion { get; set; }
        public bool ThrowBeforeEffect { get; set; }

        public ValueTask<ArtifactContentWrite> StoreIfAbsentAsync(Uuid tenantId, Stream content,
            CancellationToken ct = default) => inner.StoreIfAbsentAsync(tenantId, content, ct);

        public ValueTask<Stream?> OpenVerifiedAsync(ArtifactContentReference content,
            CancellationToken ct = default) => inner.OpenVerifiedAsync(content, ct);

        public ValueTask<ArtifactDelivery?> IssueDeliveryAsync(ArtifactContentReference content, TimeSpan lifetime,
            CancellationToken ct = default) => inner.IssueDeliveryAsync(content, lifetime, ct);

        public ValueTask<bool> PlaceHoldAsync(ArtifactContentReference content, Uuid holdId,
            CancellationToken ct = default) => inner.PlaceHoldAsync(content, holdId, ct);

        public ValueTask<bool> RemoveHoldAsync(ArtifactContentReference content, Uuid holdId,
            CancellationToken ct = default) => inner.RemoveHoldAsync(content, holdId, ct);

        public async ValueTask<bool> QuarantineAsync(ArtifactContentReference content, CancellationToken ct = default)
        {
            if (ThrowBeforeEffect)
                throw new IOException("The storage service is unavailable.");
            if (RefuseQuarantine)
                return false;
            var applied = await inner.QuarantineAsync(content, ct);
            if (ThrowAfterQuarantine)
                throw new IOException("Quarantine acknowledgement was interrupted.");
            return applied;
        }

        public ValueTask<bool> ReleaseQuarantineAsync(ArtifactContentReference content,
            CancellationToken ct = default) => inner.ReleaseQuarantineAsync(content, ct);

        public ValueTask<Stream?> OpenQuarantinedAsync(ArtifactContentReference content,
            CancellationToken ct = default) => inner.OpenQuarantinedAsync(content, ct);

        public async ValueTask<ArtifactDeletionResult> DeleteAsync(ArtifactContentReference content,
            CancellationToken ct = default)
        {
            if (ThrowBeforeEffect)
                throw new IOException("The storage service is unavailable.");
            if (RefuseDeletion)
                return ArtifactDeletionResult.Held;
            var result = await inner.DeleteAsync(content, ct);
            if (ThrowAfterDeletion)
                throw new IOException("Deletion acknowledgement was interrupted.");
            return result;
        }
    }

    sealed class InterruptedEventStore(IEventStore inner) : IEventStore
    {
        public bool FailRegistration { get; set; }
        public bool FailInspection { get; set; }

        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            CancellationToken ct) => inner.ReadAsync(stream, fromOffset, ct);

        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct) => inner.ReadAsync(pattern, cursor, ct);

        public ValueTask AppendAsync(EventStreamAddress stream, ulong expectedStreamPosition,
            IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
        {
            if (FailRegistration && events.Any(ev => ev is EvidenceArtifactRegistered) ||
                FailInspection && events.Any(ev => ev is EvidenceArtifactInspected))
                throw new IOException("The event append was interrupted.");
            return inner.AppendAsync(stream, expectedStreamPosition, events, ct);
        }
    }

    sealed class ReleaseBeforeReconciliationWriter(IAggregateWriter inner, IAggregateReader reader,
        IArtifactContentStore store, Uuid tenantId) : IAggregateWriter
    {
        public async ValueTask SaveAsync<TAggregate>(TAggregate aggregate, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            await inner.SaveAsync(aggregate, context, ct);
            if (aggregate is not EvidenceArtifact { State: EvidenceArtifactStates.Quarantined } inspected)
                return;
            var current = await reader.HydrateAsync(new EvidenceArtifact(tenantId, inspected.Id), ct);
            Assert.NotNull(current.ContentLength);
            var reference = new ArtifactContentReference(tenantId, current.ContentSha256!, current.ContentLength.Value);
            Assert.True(await store.QuarantineAsync(reference, ct));
            Assert.True(await store.ReleaseQuarantineAsync(reference, ct));
            Assert.Null(current.ReleaseQuarantine("False positive cleared.", Collector, DateTimeOffset.UtcNow));
            await inner.SaveAsync(current, context, ct);
        }
    }
}
