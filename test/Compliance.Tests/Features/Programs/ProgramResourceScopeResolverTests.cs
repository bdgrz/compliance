using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Programs;

public sealed class ProgramResourceScopeResolverTests
{
    [Fact]
    public async Task ShouldHideTenantResourceGivenOwningProgramIsNotInTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var foreignProgramId = Uuid.CreateVersion4();
        var resolver = new ProgramResourceScopeResolver(new MissingProgramDirectory(),
            new BoundaryDirectory(new BoundaryView(tenantId, boundaryId, foreignProgramId,
                null, null, null)), null!, null!, new SourceReader());

        // Act
        var programId = await resolver.ResolveProgramIdAsync(tenantId,
            new GetBoundary(tenantId, boundaryId));

        // Assert
        Assert.Null(programId);
    }

    [Fact]
    public async Task ShouldResolveProgramScopeGivenSourceCommittedBeforeProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var source = new ComplianceProgram(tenantId, programId);
        var failure = source.Create("Program", new ProgramPlan(null, null, null, null, null, null),
            Uuid.CreateVersion4(), "Admin", DateTimeOffset.UtcNow);
        Assert.Null(failure);
        var resolver = new ProgramResourceScopeResolver(new MissingProgramDirectory(),
            new BoundaryDirectory(new BoundaryView(tenantId, boundaryId, programId,
                null, null, null)), null!, null!, new SourceReader(source));

        // Act
        var explicitProgram = await resolver.IsTenantProgramAsync(tenantId, programId);
        var resourceProgram = await resolver.ResolveProgramIdAsync(tenantId,
            new GetBoundary(tenantId, boundaryId));

        // Assert
        Assert.True(explicitProgram);
        Assert.Equal(programId, resourceProgram);
    }

    [Fact]
    public async Task ShouldResolveNewResourcesGivenSourceCommittedBeforeProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var snapshotId = Uuid.CreateVersion4();
        var program = CreatedProgram(tenantId, programId);
        var boundary = new SystemBoundary(tenantId, boundaryId);
        Assert.True(boundary.Create(programId, Uuid.CreateVersion4(),
            new BoundaryContent("System boundary", "readiness", ["security"], []), actorId,
            "Admin", DateTimeOffset.UtcNow).IsSuccess);
        var service = new ClientService(tenantId, serviceId);
        Assert.True(service.Create(programId, "Service", "Purpose", "Owner", actorId,
            "Admin", DateTimeOffset.UtcNow).IsSuccess);
        var manifest = new ProgramScopeManifest(1, tenantId, programId, 1,
            new string('a', 64), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            new string('b', 64));
        var (json, digest) = SnapshotContentIdentity.Manifest(manifest);
        var snapshot = new ImmutableSnapshot(tenantId, snapshotId);
        Assert.True(snapshot.Freeze(snapshotId, null, programId, manifest, json, digest,
            null, actorId, "Admin", DateTimeOffset.UtcNow).IsSuccess);
        var resolver = new ProgramResourceScopeResolver(new MissingProgramDirectory(),
            new BoundaryDirectory(null), new MissingClientServiceDirectory(),
            new MissingSnapshotDirectory(), new SourceReader(program, boundary, service, snapshot));

        // Act
        var boundaryProgram = await resolver.ResolveProgramIdAsync(tenantId,
            new GetBoundary(tenantId, boundaryId));
        var serviceProgram = await resolver.ResolveProgramIdAsync(tenantId,
            new GetClientService(tenantId, serviceId));
        var snapshotProgram = await resolver.ResolveProgramIdAsync(tenantId,
            new GetSnapshot(tenantId, snapshotId));

        // Assert
        Assert.Equal(programId, boundaryProgram);
        Assert.Equal(programId, serviceProgram);
        Assert.Equal(programId, snapshotProgram);
    }

    [Fact]
    public async Task ShouldHideForeignSourceGivenSameResourceIdInAnotherTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var foreignTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var foreignProgram = CreatedProgram(foreignTenantId, programId);
        var foreignBoundary = new SystemBoundary(foreignTenantId, boundaryId);
        Assert.True(foreignBoundary.Create(programId, Uuid.CreateVersion4(),
            new BoundaryContent("System boundary", "readiness", ["security"], []),
            Uuid.CreateVersion4(), "Admin", DateTimeOffset.UtcNow).IsSuccess);
        var resolver = new ProgramResourceScopeResolver(new MissingProgramDirectory(),
            new BoundaryDirectory(null), null!, null!,
            new SourceReader(foreignProgram, foreignBoundary));

        // Act
        var programExists = await resolver.IsTenantProgramAsync(tenantId, programId);
        var boundaryProgram = await resolver.ResolveProgramIdAsync(tenantId,
            new GetBoundary(tenantId, boundaryId));

        // Assert
        Assert.False(programExists);
        Assert.Null(boundaryProgram);
    }

    static ComplianceProgram CreatedProgram(Uuid tenantId, Uuid programId)
    {
        var source = new ComplianceProgram(tenantId, programId);
        Assert.Null(source.Create("Program", new ProgramPlan(null, null, null, null, null, null),
            Uuid.CreateVersion4(), "Admin", DateTimeOffset.UtcNow));
        return source;
    }

    sealed class MissingProgramDirectory : IProgramDirectoryReader
    {
        public ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId,
            CancellationToken ct = default) => ValueTask.FromResult<ProgramView?>(null);

        public ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<ProgramView>([], null));

        public ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<ProgramRevisionView>?>(null);

        public ValueTask<ProgramRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid programId,
            long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<ProgramRevisionView?>(null);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class BoundaryDirectory(BoundaryView? boundary) : IBoundaryDirectoryReader
    {
        public ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId,
            CancellationToken ct = default) => ValueTask.FromResult<BoundaryView?>(boundary);

        public ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<BoundaryView>([], null));

        public ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId, Uuid boundaryId,
            Uuid versionId, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryVersionView?>(null);

        public ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<BoundaryVersionView>?>(null);

        public ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId,
            Uuid boundaryId, DateOnly effectiveOn, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryVersionView?>(null);

        public ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId,
            Uuid boundaryId, Uuid decisionId, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryDecisionView?>(null);

        public ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<BoundaryDecisionView>?>(null);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class MissingClientServiceDirectory : IClientServiceDirectoryReader
    {
        public ValueTask<ClientServiceView?> GetAsync(Uuid tenantId, Uuid serviceId,
            CancellationToken ct = default) => ValueTask.FromResult<ClientServiceView?>(null);

        public ValueTask<Page<ClientServiceView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<ClientServiceView>([], null));

        public ValueTask<Page<ClientServiceView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<ClientServiceView>([], null));

        public ValueTask<Page<ClientServiceRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid serviceId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<ClientServiceRevisionView>?>(null);

        public ValueTask<ClientServiceRevisionView?> GetRevisionAsync(Uuid tenantId,
            Uuid serviceId, long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<ClientServiceRevisionView?>(null);
    }

    sealed class MissingSnapshotDirectory : ISnapshotDirectoryReader
    {
        public ValueTask<SnapshotView?> GetAsync(Uuid tenantId, Uuid snapshotId,
            CancellationToken ct = default) => ValueTask.FromResult<SnapshotView?>(null);

        public ValueTask<Page<SnapshotView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<SnapshotView>([], null));
    }

    sealed class SourceReader(params Aggregate[] sources) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            var source = sources.FirstOrDefault(source => source.GetType() == aggregate.GetType() &&
                source.Stream == aggregate.Stream);
            return ValueTask.FromResult(source is not null ? (TAggregate)source : aggregate);
        }
    }
}
