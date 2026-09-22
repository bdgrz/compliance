using System.Security.Cryptography;
using Bdgrz.Compliance.Features.Artifacts;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Tests.Features.Artifacts;

public sealed class LocalArtifactContentStoreTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), $"bdgrz-artifacts-{Guid.NewGuid():N}");
    readonly MutableTimeProvider _time = new(new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task ShouldReturnExistingIdentityGivenIdenticalContentInSameTenant()
    {
        // Arrange
        var store = Store();
        var tenantId = Uuid.CreateVersion4();
        var bytes = "evidence"u8.ToArray();

        // Act
        var first = await store.StoreIfAbsentAsync(tenantId, new MemoryStream(bytes));
        var second = await store.StoreIfAbsentAsync(tenantId, new MemoryStream(bytes));

        // Assert
        Assert.False(first.AlreadyPresent);
        Assert.True(second.AlreadyPresent);
        Assert.Equal(first.Content, second.Content);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), first.Content.Sha256);
        Assert.Equal(bytes.Length, first.Content.Length);
        Assert.Equal(tenantId, first.Content.TenantId);
    }

    [Fact]
    public async Task ShouldNotDeduplicateGivenIdenticalContentInAnotherTenant()
    {
        // Arrange
        var store = Store();
        var tenantA = Uuid.CreateVersion4();
        var tenantB = Uuid.CreateVersion4();
        var bytes = "shared evidence"u8.ToArray();
        var written = await store.StoreIfAbsentAsync(tenantA, new MemoryStream(bytes));
        var crossTenant = written.Content with { TenantId = tenantB };

        // Act
        await using var beforeWrite = await store.OpenVerifiedAsync(crossTenant);
        var tenantBWrite = await store.StoreIfAbsentAsync(tenantB, new MemoryStream(bytes));

        // Assert
        Assert.Null(beforeWrite);
        Assert.False(tenantBWrite.AlreadyPresent);
        Assert.Equal(crossTenant, tenantBWrite.Content);
    }

    [Fact]
    public async Task ShouldReturnStoredBytesGivenVerifiedRead()
    {
        // Arrange
        var store = Store();
        var tenantId = Uuid.CreateVersion4();
        var bytes = "policy file"u8.ToArray();
        var written = await store.StoreIfAbsentAsync(tenantId, new MemoryStream(bytes));

        // Act
        await using var read = await store.OpenVerifiedAsync(written.Content);

        // Assert
        Assert.NotNull(read);
        Assert.Equal(bytes, await ReadAllAsync(read));
    }

    [Fact]
    public async Task ShouldRejectReadGivenStoredBytesChanged()
    {
        // Arrange
        var store = Store();
        var tenantId = Uuid.CreateVersion4();
        var written = await store.StoreIfAbsentAsync(tenantId, new MemoryStream("original"u8.ToArray()));
        var file = Directory.EnumerateFiles(_root, written.Content.Sha256, SearchOption.AllDirectories).Single();
        await File.WriteAllBytesAsync(file, "tampered"u8.ToArray());

        // Act
        var failure = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await store.OpenVerifiedAsync(written.Content));

        // Assert
        Assert.Contains("verification", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldRejectReadGivenLengthDiffersFromStoredContent()
    {
        // Arrange
        var store = Store();
        var tenantId = Uuid.CreateVersion4();
        var written = await store.StoreIfAbsentAsync(tenantId, new MemoryStream("sized"u8.ToArray()));

        // Act
        var failure = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await store.OpenVerifiedAsync(written.Content with { Length = written.Content.Length + 1 }));

        // Assert
        Assert.Contains("verification", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldPlaceContentGivenTenantDigestLayout()
    {
        // Arrange
        var store = Store();
        var tenantId = Uuid.CreateVersion4();

        // Act
        var written = await store.StoreIfAbsentAsync(tenantId, new MemoryStream("layout"u8.ToArray()));

        // Assert
        var digest = written.Content.Sha256;
        Assert.True(File.Exists(Path.Combine(_root, tenantId.ToString(), "content", "sha256", digest[..2], digest)));
    }

    [Fact]
    public async Task ShouldRejectContentGivenLengthAboveLimit()
    {
        // Arrange
        var store = Store(maxContentLength: 4);
        var tenantId = Uuid.CreateVersion4();

        // Act
        var failure = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await store.StoreIfAbsentAsync(tenantId, new MemoryStream("too long"u8.ToArray())));

        // Assert
        Assert.Equal("content", failure.ParamName);
        Assert.Empty(Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("../../escape")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("abc")]
    public async Task ShouldRejectReferenceGivenNonCanonicalDigest(string digest)
    {
        // Arrange
        var store = Store();
        var reference = new ArtifactContentReference(Uuid.CreateVersion4(), digest, 1);

        // Act
        var failure = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await store.OpenVerifiedAsync(reference));

        // Assert
        Assert.Equal("content", failure.ParamName);
    }

    [Fact]
    public async Task ShouldOpenDeliveryGivenUnexpiredLocation()
    {
        // Arrange
        var store = Store();
        var tenantId = Uuid.CreateVersion4();
        var bytes = "delivered"u8.ToArray();
        var written = await store.StoreIfAbsentAsync(tenantId, new MemoryStream(bytes));
        var delivery = await store.IssueDeliveryAsync(written.Content, TimeSpan.FromMinutes(1));
        _time.Advance(TimeSpan.FromSeconds(59));

        // Act
        await using var opened = await store.OpenDeliveryAsync(delivery!.Location);

        // Assert
        Assert.Equal(_time.Start.AddMinutes(1), delivery.ExpiresAt);
        Assert.NotNull(opened);
        Assert.Equal(bytes, await ReadAllAsync(opened));
    }

    [Fact]
    public async Task ShouldRejectDeliveryGivenExpiredLocation()
    {
        // Arrange
        var store = Store();
        var written = await store.StoreIfAbsentAsync(Uuid.CreateVersion4(), new MemoryStream("late"u8.ToArray()));
        var delivery = await store.IssueDeliveryAsync(written.Content, TimeSpan.FromMinutes(1));
        _time.Advance(TimeSpan.FromMinutes(1));

        // Act
        await using var opened = await store.OpenDeliveryAsync(delivery!.Location);

        // Assert
        Assert.Null(opened);
    }

    [Fact]
    public async Task ShouldRejectDeliveryGivenLocationSignedByAnotherKey()
    {
        // Arrange
        var issuer = Store();
        var written = await issuer.StoreIfAbsentAsync(Uuid.CreateVersion4(), new MemoryStream("forged"u8.ToArray()));
        var delivery = await issuer.IssueDeliveryAsync(written.Content, TimeSpan.FromMinutes(1));
        var otherKey = Store(deliveryKey: RandomNumberGenerator.GetBytes(32));

        // Act
        await using var opened = await otherKey.OpenDeliveryAsync(delivery!.Location);

        // Assert
        Assert.Null(opened);
    }

    [Fact]
    public async Task ShouldRejectDeliveryGivenLifetimeAboveMaximum()
    {
        // Arrange
        var store = Store();
        var written = await store.StoreIfAbsentAsync(Uuid.CreateVersion4(), new MemoryStream("long"u8.ToArray()));

        // Act
        var failure = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await store.IssueDeliveryAsync(written.Content,
                ArtifactContentLimits.MaxDeliveryLifetime + TimeSpan.FromSeconds(1)));

        // Assert
        Assert.Equal("lifetime", failure.ParamName);
    }

    [Fact]
    public async Task ShouldNotIssueDeliveryGivenContentAbsentFromTenant()
    {
        // Arrange
        var store = Store();
        var written = await store.StoreIfAbsentAsync(Uuid.CreateVersion4(), new MemoryStream("absent"u8.ToArray()));

        // Act
        var delivery = await store.IssueDeliveryAsync(written.Content with { TenantId = Uuid.CreateVersion4() },
            TimeSpan.FromMinutes(1));

        // Assert
        Assert.Null(delivery);
    }

    [Fact]
    public async Task ShouldDeleteOnlyReferencedTenantContentGivenDispositionHook()
    {
        // Arrange
        var store = Store();
        var tenantA = Uuid.CreateVersion4();
        var tenantB = Uuid.CreateVersion4();
        var bytes = "dispose"u8.ToArray();
        var a = await store.StoreIfAbsentAsync(tenantA, new MemoryStream(bytes));
        var b = await store.StoreIfAbsentAsync(tenantB, new MemoryStream(bytes));

        // Act
        var deleted = await store.DeleteAsync(a.Content);
        var repeated = await store.DeleteAsync(a.Content);

        // Assert
        Assert.Equal(ArtifactDeletionResult.Deleted, deleted);
        Assert.Equal(ArtifactDeletionResult.NotFound, repeated);
        await using var goneA = await store.OpenVerifiedAsync(a.Content);
        await using var keptB = await store.OpenVerifiedAsync(b.Content);
        Assert.Null(goneA);
        Assert.NotNull(keptB);
    }

    [Fact]
    public async Task ShouldRepairCorruptObjectGivenVerifiedIdenticalUpload()
    {
        // Arrange
        var store = Store();
        var tenantId = Uuid.CreateVersion4();
        var bytes = "repairable"u8.ToArray();
        var written = await store.StoreIfAbsentAsync(tenantId, new MemoryStream(bytes));
        var file = Directory.EnumerateFiles(_root, written.Content.Sha256, SearchOption.AllDirectories).Single();
        await File.WriteAllBytesAsync(file, "truncated"u8.ToArray());

        // Act
        var repaired = await store.StoreIfAbsentAsync(tenantId, new MemoryStream(bytes));

        // Assert
        Assert.False(repaired.AlreadyPresent);
        await using var read = await store.OpenVerifiedAsync(written.Content);
        Assert.Equal(bytes, await ReadAllAsync(read!));
    }

    [Fact]
    public async Task ShouldNotIssueDeliveryGivenLengthDiffersFromStoredContent()
    {
        // Arrange
        var store = Store();
        var written = await store.StoreIfAbsentAsync(Uuid.CreateVersion4(), new MemoryStream("length"u8.ToArray()));

        // Act
        var delivery = await store.IssueDeliveryAsync(written.Content with { Length = written.Content.Length + 1 },
            TimeSpan.FromMinutes(1));

        // Assert
        Assert.Null(delivery);
    }

    [Fact]
    public async Task ShouldNotDeleteGivenLengthDiffersFromStoredContent()
    {
        // Arrange
        var store = Store();
        var written = await store.StoreIfAbsentAsync(Uuid.CreateVersion4(), new MemoryStream("keep"u8.ToArray()));

        // Act
        var deleted = await store.DeleteAsync(written.Content with { Length = written.Content.Length + 1 });

        // Assert
        Assert.Equal(ArtifactDeletionResult.NotFound, deleted);
        await using var kept = await store.OpenVerifiedAsync(written.Content);
        Assert.NotNull(kept);
    }

    [Fact]
    public async Task ShouldReportExpiryMatchingSignedPrecisionGivenSubMillisecondClock()
    {
        // Arrange
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero).AddTicks(7_000));
        var store = new LocalArtifactContentStore(
            new ArtifactContentStoreOptions(_root, Key, ArtifactContentLimits.MaxContentLength), time);
        var written = await store.StoreIfAbsentAsync(Uuid.CreateVersion4(), new MemoryStream("precise"u8.ToArray()));
        var delivery = await store.IssueDeliveryAsync(written.Content, TimeSpan.FromMinutes(1));
        time.Advance(delivery!.ExpiresAt - time.GetUtcNow() - TimeSpan.FromTicks(1));

        // Act
        await using var opened = await store.OpenDeliveryAsync(delivery.Location);

        // Assert
        Assert.NotNull(opened);
    }

    [Fact]
    public async Task ShouldBlockDeletionGivenAnyActiveHold()
    {
        // Arrange
        var store = Store();
        var written = await store.StoreIfAbsentAsync(Uuid.CreateVersion4(), new MemoryStream("held"u8.ToArray()));
        var engagementHold = Uuid.CreateVersion4();
        var legalHold = Uuid.CreateVersion4();
        Assert.True(await store.PlaceHoldAsync(written.Content, engagementHold));
        Assert.True(await store.PlaceHoldAsync(written.Content, legalHold));

        // Act
        var whileBothHeld = await store.DeleteAsync(written.Content);
        Assert.True(await store.RemoveHoldAsync(written.Content, engagementHold));
        var whileLegalHeld = await store.DeleteAsync(written.Content);
        Assert.True(await store.RemoveHoldAsync(written.Content, legalHold));
        var released = await store.DeleteAsync(written.Content);

        // Assert
        Assert.Equal(ArtifactDeletionResult.Held, whileBothHeld);
        Assert.Equal(ArtifactDeletionResult.Held, whileLegalHeld);
        Assert.Equal(ArtifactDeletionResult.Deleted, released);
    }

    [Fact]
    public async Task ShouldNotPlaceHoldGivenContentAbsentFromTenant()
    {
        // Arrange
        var store = Store();
        var written = await store.StoreIfAbsentAsync(Uuid.CreateVersion4(), new MemoryStream("absent"u8.ToArray()));

        // Act
        var placed = await store.PlaceHoldAsync(written.Content with { TenantId = Uuid.CreateVersion4() },
            Uuid.CreateVersion4());

        // Assert
        Assert.False(placed);
    }

    [Fact]
    public async Task ShouldWithholdQuarantinedContentGivenOrdinaryReadAndDelivery()
    {
        // Arrange
        var store = Store();
        var bytes = "flagged"u8.ToArray();
        var written = await store.StoreIfAbsentAsync(Uuid.CreateVersion4(), new MemoryStream(bytes));

        // Act
        var quarantined = await store.QuarantineAsync(written.Content);
        await using var ordinary = await store.OpenVerifiedAsync(written.Content);
        var delivery = await store.IssueDeliveryAsync(written.Content, TimeSpan.FromMinutes(1));
        await using var review = await store.OpenQuarantinedAsync(written.Content);

        // Assert
        Assert.True(quarantined);
        Assert.Null(ordinary);
        Assert.Null(delivery);
        Assert.NotNull(review);
        Assert.Equal(bytes, await ReadAllAsync(review));
    }

    [Fact]
    public async Task ShouldKeepQuarantineGivenIdenticalUpload()
    {
        // Arrange
        var store = Store();
        var tenantId = Uuid.CreateVersion4();
        var bytes = "flagged again"u8.ToArray();
        var written = await store.StoreIfAbsentAsync(tenantId, new MemoryStream(bytes));
        await store.QuarantineAsync(written.Content);

        // Act
        var repeated = await store.StoreIfAbsentAsync(tenantId, new MemoryStream(bytes));
        await using var ordinary = await store.OpenVerifiedAsync(written.Content);

        // Assert
        Assert.True(repeated.AlreadyPresent);
        Assert.Null(ordinary);
    }

    [Fact]
    public async Task ShouldRestoreDeliveryGivenQuarantineReleased()
    {
        // Arrange
        var store = Store();
        var written = await store.StoreIfAbsentAsync(Uuid.CreateVersion4(), new MemoryStream("released"u8.ToArray()));
        await store.QuarantineAsync(written.Content);

        // Act
        var released = await store.ReleaseQuarantineAsync(written.Content);
        await using var ordinary = await store.OpenVerifiedAsync(written.Content);
        await using var review = await store.OpenQuarantinedAsync(written.Content);

        // Assert
        Assert.True(released);
        Assert.NotNull(ordinary);
        Assert.Null(review);
    }

    [Fact]
    public async Task ShouldDeleteQuarantinedContentGivenNoHold()
    {
        // Arrange
        var store = Store();
        var written = await store.StoreIfAbsentAsync(Uuid.CreateVersion4(), new MemoryStream("disposed"u8.ToArray()));
        await store.QuarantineAsync(written.Content);

        // Act
        var deleted = await store.DeleteAsync(written.Content);

        // Assert
        Assert.Equal(ArtifactDeletionResult.Deleted, deleted);
        await using var review = await store.OpenQuarantinedAsync(written.Content);
        Assert.Null(review);
    }

    [Fact]
    public void ShouldRejectConfigurationGivenShortDeliveryKey()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Artifacts:LocalDeliveryKey"] = Convert.ToBase64String(new byte[16]),
            })
            .Build();

        // Act
        var failure = Assert.Throws<InvalidOperationException>(() =>
            ArtifactContentStoreOptions.FromConfiguration(configuration));

        // Assert
        Assert.Contains("32 bytes", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldRejectConfigurationGivenRelativeContentRoot()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Artifacts:LocalContentRoot"] = "relative/artifacts",
            })
            .Build();

        // Act
        var failure = Assert.Throws<InvalidOperationException>(() =>
            ArtifactContentStoreOptions.FromConfiguration(configuration));

        // Assert
        Assert.Contains("absolute", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldFailClosedGivenNoConfiguredRoot()
    {
        // Arrange
        var store = new LocalArtifactContentStore(
            new ArtifactContentStoreOptions(null, null, ArtifactContentLimits.MaxContentLength), _time);

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.StoreIfAbsentAsync(Uuid.CreateVersion4(), new MemoryStream("x"u8.ToArray())));

        // Assert
        Assert.Contains("Artifacts:LocalContentRoot", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldReadLocalSettingsGivenConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Artifacts:LocalContentRoot"] = _root,
                ["Artifacts:LocalDeliveryKey"] = Convert.ToBase64String(Key),
            })
            .Build();

        // Act
        var options = ArtifactContentStoreOptions.FromConfiguration(configuration);

        // Assert
        Assert.Equal(_root, options.LocalRoot);
        Assert.Equal(Key, options.DeliveryKey);
        Assert.Equal(ArtifactContentLimits.MaxContentLength, options.MaxContentLength);
    }

    [Fact]
    public void ShouldRejectConfigurationGivenMalformedDeliveryKey()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Artifacts:LocalDeliveryKey"] = "not base64!",
            })
            .Build();

        // Act
        var failure = Assert.Throws<InvalidOperationException>(() =>
            ArtifactContentStoreOptions.FromConfiguration(configuration));

        // Assert
        Assert.Contains("Artifacts:LocalDeliveryKey", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReportNotInspectedGivenDefaultInspector()
    {
        // Arrange
        var inspector = new UninspectedArtifactInspector();
        var reference = new ArtifactContentReference(Uuid.CreateVersion4(), new string('a', 64), 1);

        // Act
        var result = await inspector.InspectAsync(reference);

        // Assert
        Assert.Equal(ArtifactInspectionState.NotInspected, result.State);
    }

    LocalArtifactContentStore Store(long maxContentLength = ArtifactContentLimits.MaxContentLength,
        byte[]? deliveryKey = null) =>
        new(new ArtifactContentStoreOptions(_root, deliveryKey ?? Key, maxContentLength), _time);

    static readonly byte[] Key = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();

    static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        DateTimeOffset _now = start;

        public DateTimeOffset Start { get; } = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
