using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Snapshots;

public sealed class ScopeSnapshotTests
{
    [Fact]
    public void ShouldRetainOriginalGivenAttributableAmendment()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var initialId = Uuid.CreateVersion4();
        var amendedId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var manifest = Manifest(tenantId, programId);
        var (json, digest) = SnapshotContentIdentity.Manifest(manifest);
        var revisedManifest = manifest with { ProgramRevision = 2 };
        var (revisedJson, revisedDigest) = SnapshotContentIdentity.Manifest(revisedManifest);
        var initial = new ImmutableSnapshot(tenantId, initialId);
        var amendment = new ImmutableSnapshot(tenantId, amendedId);
        var now = DateTimeOffset.UtcNow;

        // Act
        var first = initial.Freeze(initialId, null, programId, manifest, json, digest,
            null, actorId, "Lead", now);
        var repeated = initial.Freeze(initialId, null, programId, manifest, json, digest,
            null, actorId, "Lead", now);
        var forged = amendment.Freeze(initialId, initialId, programId,
            revisedManifest, json, "different-content-digest",
            "Corrected scope", actorId, "Lead", now.AddMinutes(1));
        var unsupported = amendment.Freeze(initialId, initialId, programId,
            manifest with { FormatVersion = 2 }, json, digest,
            "Corrected scope", actorId, "Lead", now.AddMinutes(1));
        var revised = amendment.Freeze(initialId, initialId, programId,
            revisedManifest, revisedJson, revisedDigest,
            "  Corrected scope  ", actorId, "Lead", now.AddMinutes(1));
        var retried = amendment.Freeze(initialId, initialId, programId,
            revisedManifest, revisedJson, revisedDigest,
            "Corrected scope", actorId, "Lead", now.AddMinutes(1));
        var changed = amendment.Freeze(initialId, initialId, programId,
            manifest, json, digest, "Corrected scope", actorId, "Lead", now);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(forged.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(unsupported.Error).Kind);
        Assert.True(revised.IsSuccess);
        Assert.True(retried.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(changed.Error).Kind);
        Assert.True(initial.IsFrozen);
        Assert.Equal(initialId, initial.RootSnapshotId);
        Assert.Equal(initialId, amendment.RootSnapshotId);
        Assert.Equal(programId, amendment.ProgramId);
    }

    [Fact]
    public void ShouldHashCanonicalSourceGivenEquivalentUnicodeAndDifferentVersions()
    {
        // Arrange
        var programId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var plan = new ProgramPlan(null, null, null, null, null, null);
        var first = new ProgramRevisionView(programId, 1, "Caf\u00e9", plan,
            memberId, "Lead", now);
        var equivalent = first with { Name = "Cafe\u0301" };
        var revised = first with { Revision = 2 };

        // Act
        var firstHash = SnapshotContentIdentity.ProgramRevision(first);
        var equivalentHash = SnapshotContentIdentity.ProgramRevision(equivalent);
        var revisedHash = SnapshotContentIdentity.ProgramRevision(revised);

        // Assert
        Assert.Equal(firstHash, equivalentHash);
        Assert.NotEqual(firstHash, revisedHash);
        Assert.Equal(64, firstHash.Length);
    }

    [Fact]
    public void ShouldRejectChangedStoredManifestGivenOriginalDigest()
    {
        // Arrange
        var manifest = Manifest(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var (json, digest) = SnapshotContentIdentity.Manifest(manifest);

        // Act
        var changedBytes = SnapshotContentIdentity.MatchesManifest(manifest,
            json.Replace("program_scope", "other_scope", StringComparison.Ordinal), digest);
        var changedDigest = SnapshotContentIdentity.MatchesManifest(manifest, json,
            new string('f', 64));
        var untouched = SnapshotContentIdentity.MatchesManifest(manifest, json, digest);

        // Assert
        Assert.False(changedBytes);
        Assert.False(changedDigest);
        Assert.True(untouched);
    }

    [Fact]
    public async Task ShouldProjectTenantScopedImmutableSnapshotGivenFitzBatch()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var snapshotId = Uuid.CreateVersion4();
        var manifest = Manifest(tenantId, programId);
        var (json, digest) = SnapshotContentIdentity.Manifest(manifest);
        var snapshot = new ImmutableSnapshot(tenantId, snapshotId);
        Assert.True(snapshot.Freeze(snapshotId, null, programId, manifest, json,
            digest, null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var frozen = Assert.Single(new AggregateScenario<ImmutableSnapshot>(snapshot).PendingEvents);
        var directory = new FitzSnapshotDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("SnapshotDirectory",
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var uncommitted = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
            await directory.ApplyAsync(frozen);
        var rolledBack = await directory.GetAsync(tenantId, snapshotId);
        await using (var committed = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(frozen);
            await committed.CommitAsync(ProjectionCheckpoint.Start);
        }
        var current = await directory.GetAsync(tenantId, snapshotId);
        var page = await directory.ListProgramAsync(tenantId, programId, 1, null);
        var foreign = await directory.GetAsync(otherTenantId, snapshotId);

        // Assert
        Assert.Null(rolledBack);
        Assert.Equal(digest, current?.ContentSha256);
        Assert.Equal(json, current?.CanonicalManifest);
        Assert.Equal(snapshotId, Assert.Single(page.Items).SnapshotId);
        Assert.Null(foreign);
    }

    static ProgramScopeManifest Manifest(Uuid tenantId, Uuid programId) =>
        new(1, tenantId, programId, 1, new string('a', 64), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), new string('b', 64));
}
