using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Snapshots;

public sealed class SnapshotManifestImpactTests
{
    [Fact]
    public void ShouldIdentifyChangedSourcesGivenAmendedManifest()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var original = new ProgramScopeManifest(1, tenantId, programId, 3, new string('a', 64),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), new string('b', 64));
        var programOnly = original with { ProgramRevision = 4, ProgramContentSha256 = new string('c', 64) };
        var boundaryOnly = original with
        {
            ApprovedBoundaryVersionId = Uuid.CreateVersion4(),
            BoundaryContentSha256 = new string('d', 64)
        };
        var rehashedBoundary = original with { BoundaryContentSha256 = new string('e', 64) };

        // Act
        var unchanged = SnapshotManifestImpact.ChangedSources(original, original);
        var program = SnapshotManifestImpact.ChangedSources(original, programOnly);
        var boundary = SnapshotManifestImpact.ChangedSources(original, boundaryOnly);
        var rehashed = SnapshotManifestImpact.ChangedSources(original, rehashedBoundary);

        // Assert
        Assert.Empty(unchanged.Value!);
        Assert.Equal(["program_revision"], program.Value);
        Assert.Equal(["approved_boundary_version"], boundary.Value);
        Assert.Equal(["approved_boundary_version"], rehashed.Value);
    }

    [Fact]
    public void ShouldRejectComparisonGivenDifferentProgramScope()
    {
        // Arrange
        var original = new ProgramScopeManifest(1, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1,
            new string('a', 64), Uuid.CreateVersion4(), Uuid.CreateVersion4(), new string('b', 64));
        var otherTenant = original with { TenantId = Uuid.CreateVersion4() };
        var otherProgram = original with { ProgramId = Uuid.CreateVersion4() };
        var otherFormat = original with { FormatVersion = 2 };

        // Act
        var results = new[]
        {
            SnapshotManifestImpact.ChangedSources(original, otherTenant),
            SnapshotManifestImpact.ChangedSources(original, otherProgram),
            SnapshotManifestImpact.ChangedSources(original, otherFormat)
        };

        // Assert
        Assert.All(results, result => Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(result.Error).Kind));
    }
}
