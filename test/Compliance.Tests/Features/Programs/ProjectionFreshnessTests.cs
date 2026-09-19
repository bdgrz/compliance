using System.Security.Claims;
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

    sealed class EmptyProgramDirectory : IProgramDirectoryReader
    {
        public ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId,
            CancellationToken ct = default) => ValueTask.FromResult<ProgramView?>(null);

        public ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<ProgramView>([], null));

        public ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<ProgramRevisionView>?>(null);
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
