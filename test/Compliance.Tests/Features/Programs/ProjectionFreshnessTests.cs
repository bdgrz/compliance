using System.Security.Claims;
using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Programs;

public sealed class ProjectionFreshnessTests
{
    [Fact]
    public async Task ShouldReportLagGivenProgramEventExistsBeforeProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var program = new ComplianceProgram(tenantId, programId);
        Assert.True(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null),
            Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var handler = new GetProgramHandler(new EmptyProgramDirectory(),
            new SourceReader(program));

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetProgram>(
            new GetProgram(tenantId, programId, 1), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.Contains("projection", error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReportLagGivenSetupWorkProgramEventExistsBeforeProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var program = new ComplianceProgram(tenantId, programId);
        Assert.True(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null),
            Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var handler = new GetProgramSetupWorkHandler(new EmptyProgramDirectory(),
            new EmptyBoundaryDirectory(), new SourceReader(program));

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetProgramSetupWork>(
            new GetProgramSetupWork(tenantId, programId, MinimumProgramRevision: 1),
            new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.Contains("projection", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReportLagGivenSetupWorkBoundaryEventExistsBeforeProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var boundary = new SystemBoundary(tenantId, boundaryId);
        Assert.True(boundary.Create(programId, Uuid.CreateVersion4(),
            new BoundaryContent("Scope", "readiness", ["security"], []),
            Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var program = new ProgramView(tenantId, programId, "SOC 2", "readiness", "type_i",
            1, new ProgramPlan(null, null, null, null, null, null), Uuid.CreateVersion4(),
            "Lead", DateTimeOffset.UtcNow, []);
        var handler = new GetProgramSetupWorkHandler(new EmptyProgramDirectory(program),
            new EmptyBoundaryDirectory(), new SourceReader(boundary));

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetProgramSetupWork>(
            new GetProgramSetupWork(tenantId, programId, BoundaryId: boundaryId,
                MinimumBoundaryRevision: 1), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.Contains("projection", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReportLagGivenServiceEventExistsBeforeProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var service = new ClientService(tenantId, serviceId);
        Assert.True(service.Create(Uuid.CreateVersion4(), "Payroll", "Run payroll", "Operations",
            Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var handler = new GetClientServiceHandler(new EmptyServiceDirectory(),
            new SourceReader(service));

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetClientService>(
            new GetClientService(tenantId, serviceId, 1), new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.Contains("projection", error.Message,
            StringComparison.Ordinal);
    }

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }

    sealed class EmptyProgramDirectory(ProgramView? program = null) : IProgramDirectoryReader
    {
        public ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId,
            CancellationToken ct = default) => ValueTask.FromResult(program);

        public ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<ProgramView>([], null));

        public ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<ProgramRevisionView>?>(null);
    }

    sealed class EmptyBoundaryDirectory : IBoundaryDirectoryReader
    {
        public ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId,
            CancellationToken ct = default) => ValueTask.FromResult<BoundaryView?>(null);

        public ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<BoundaryView>([], null));

        public ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId, Uuid boundaryId,
            Uuid versionId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId,
            Uuid boundaryId, DateOnly effectiveOn, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId,
            Uuid boundaryId, Uuid decisionId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    sealed class EmptyServiceDirectory : IClientServiceDirectoryReader
    {
        public ValueTask<ClientServiceView?> GetAsync(Uuid tenantId, Uuid serviceId,
            CancellationToken ct = default) => ValueTask.FromResult<ClientServiceView?>(null);

        public ValueTask<Page<ClientServiceView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<ClientServiceView>([], null));

        public ValueTask<Page<ClientServiceView>> ListProgramAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<ClientServiceView>([], null));

        public ValueTask<Page<ClientServiceRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid serviceId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<ClientServiceRevisionView>?>(null);
    }
}
