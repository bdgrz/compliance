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
    readonly Uuid _tenantId = Uuid.CreateVersion4();

    public EvidenceIntakeTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
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

    Task<Result<EvidenceCapture>> CaptureAsync(byte[] bytes, string title = "Quarterly export") =>
        Intake().CaptureAsync(_tenantId, new MemoryStream(bytes), Metadata(title), Collector, Dispatch(),
            CancellationToken.None).AsTask();

    EvidenceIntake Intake() => new(_store, _inspector,
        _scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
        _scope.ServiceProvider.GetRequiredService<IAggregateWriter>(), TimeProvider.System);

    async Task<EvidenceArtifact> LoadAsync(Uuid artifactId) =>
        await _scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new EvidenceArtifact(_tenantId, artifactId));

    static RequestDispatchContext Dispatch() => new(RequestActor.System);

    static EvidenceArtifactContent Metadata(string title) =>
        new(title, null, "system_export", "Okta", DateTimeOffset.UtcNow.AddHours(-1),
            new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30), "confidential");

    sealed class Inspector : IArtifactInspector
    {
        public ArtifactInspectionState State { get; set; } = ArtifactInspectionState.NotInspected;
        public ArtifactContentReference? Last { get; private set; }

        public ValueTask<ArtifactInspectionResult> InspectAsync(ArtifactContentReference content,
            CancellationToken ct = default)
        {
            Last = content;
            return ValueTask.FromResult(new ArtifactInspectionResult(State));
        }
    }
}
