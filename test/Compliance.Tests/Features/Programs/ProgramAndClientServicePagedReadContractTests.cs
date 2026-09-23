using System.Security.Claims;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Programs;

public sealed class ProgramAndClientServicePagedReadContractTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ShouldRejectOutOfRangeLimitGivenProgramAndClientServiceLists(int limit)
    {
        // Arrange
        var scenario = CreateScenario();
        var programHistory = new ProgramHistoryReadConsistency(scenario.Programs, scenario.Reader);
        var serviceHistory = new ClientServiceHistoryReadConsistency(scenario.Services, scenario.Reader);
        var programs = new ListProgramsHandler(scenario.Programs);
        var programRevisions = new ListProgramRevisionsHandler(scenario.Programs, programHistory);
        var services = new ListClientServicesHandler(scenario.Services);
        var programServices = new ListProgramClientServicesHandler(scenario.Services, scenario.Reader);
        var serviceRevisions = new ListClientServiceRevisionsHandler(scenario.Services, serviceHistory);

        // Act
        var programResult = await programs.HandleAsync(Context(new ListPrograms(scenario.TenantId,
            limit)), CancellationToken.None);
        var programRevisionResult = await programRevisions.HandleAsync(Context(
            new ListProgramRevisions(scenario.TenantId, scenario.ProgramId, limit)),
            CancellationToken.None);
        var serviceResult = await services.HandleAsync(Context(new ListClientServices(scenario.TenantId,
            limit)), CancellationToken.None);
        var programServiceResult = await programServices.HandleAsync(Context(
            new ListProgramClientServices(scenario.TenantId, scenario.ProgramId, limit)),
            CancellationToken.None);
        var serviceRevisionResult = await serviceRevisions.HandleAsync(Context(
            new ListClientServiceRevisions(scenario.TenantId, scenario.ServiceId, limit)),
            CancellationToken.None);

        // Assert
        AssertValidation(programResult.Error);
        AssertValidation(programRevisionResult.Error);
        AssertValidation(serviceResult.Error);
        AssertValidation(programServiceResult.Error);
        AssertValidation(serviceRevisionResult.Error);
    }

    [Fact]
    public async Task ShouldRejectInvalidCursorGivenProgramAndClientServiceLists()
    {
        // Arrange
        var scenario = CreateScenario(rejectCursor: true);
        var programHistory = new ProgramHistoryReadConsistency(scenario.Programs, scenario.Reader);
        var serviceHistory = new ClientServiceHistoryReadConsistency(scenario.Services, scenario.Reader);
        var programs = new ListProgramsHandler(scenario.Programs);
        var programRevisions = new ListProgramRevisionsHandler(scenario.Programs, programHistory);
        var services = new ListClientServicesHandler(scenario.Services);
        var programServices = new ListProgramClientServicesHandler(scenario.Services, scenario.Reader);
        var serviceRevisions = new ListClientServiceRevisionsHandler(scenario.Services, serviceHistory);

        // Act
        var programResult = await programs.HandleAsync(Context(new ListPrograms(scenario.TenantId,
            Cursor: "invalid")), CancellationToken.None);
        var programRevisionResult = await programRevisions.HandleAsync(Context(
            new ListProgramRevisions(scenario.TenantId, scenario.ProgramId, Cursor: "invalid")),
            CancellationToken.None);
        var serviceResult = await services.HandleAsync(Context(new ListClientServices(scenario.TenantId,
            Cursor: "invalid")), CancellationToken.None);
        var programServiceResult = await programServices.HandleAsync(Context(
            new ListProgramClientServices(scenario.TenantId, scenario.ProgramId, Cursor: "invalid")),
            CancellationToken.None);
        var serviceRevisionResult = await serviceRevisions.HandleAsync(Context(
            new ListClientServiceRevisions(scenario.TenantId, scenario.ServiceId, Cursor: "invalid")),
            CancellationToken.None);

        // Assert
        AssertValidation(programResult.Error);
        AssertValidation(programRevisionResult.Error);
        AssertValidation(serviceResult.Error);
        AssertValidation(programServiceResult.Error);
        AssertValidation(serviceRevisionResult.Error);
    }

    [Fact]
    public async Task ShouldRejectMismatchedScopeGivenProgramAndClientServiceLists()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var otherProgramId = Uuid.CreateVersion4();
        var otherServiceId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var programs = new ProgramDirectory
        {
            ProgramPage = new Page<ProgramView>([Program(otherTenantId, programId)], null),
            RevisionPage = new Page<ProgramRevisionView>([
                new ProgramRevisionView(otherProgramId, 1, "Readiness", Plan(), Uuid.CreateVersion4(),
                    "Owner", now),
            ], null),
        };
        var services = new ClientServiceDirectory
        {
            ServicePage = new Page<ClientServiceView>([
                Service(otherTenantId, serviceId, programId),
            ], null),
            ProgramServicePage = new Page<ClientServiceView>([
                Service(tenantId, serviceId, otherProgramId),
            ], null),
            RevisionPage = new Page<ClientServiceRevisionView>([
                new ClientServiceRevisionView(otherServiceId, 1, "Payroll", "Run payroll",
                    "Operations", "active", null, Uuid.CreateVersion4(), "Owner", now, programId),
            ], null),
        };
        var reader = new AggregateReader(ProgramSource(tenantId, programId));
        var programHistory = new ProgramHistoryReadConsistency(programs, reader);
        var serviceHistory = new ClientServiceHistoryReadConsistency(services, reader);

        // Act
        var programResult = await new ListProgramsHandler(programs).HandleAsync(Context(
            new ListPrograms(tenantId)), CancellationToken.None);
        var programRevisionResult = await new ListProgramRevisionsHandler(programs, programHistory)
            .HandleAsync(Context(new ListProgramRevisions(tenantId, programId)), CancellationToken.None);
        var serviceResult = await new ListClientServicesHandler(services).HandleAsync(Context(
            new ListClientServices(tenantId)), CancellationToken.None);
        var programServiceResult = await new ListProgramClientServicesHandler(services, reader)
            .HandleAsync(Context(new ListProgramClientServices(tenantId, programId)),
                CancellationToken.None);
        var serviceRevisionResult = await new ListClientServiceRevisionsHandler(services, serviceHistory)
            .HandleAsync(Context(new ListClientServiceRevisions(tenantId, serviceId)),
                CancellationToken.None);

        // Assert
        AssertError(programResult.Error, RequestErrorKind.NotFound);
        AssertError(programRevisionResult.Error, RequestErrorKind.Conflict);
        AssertError(serviceResult.Error, RequestErrorKind.NotFound);
        AssertError(programServiceResult.Error, RequestErrorKind.NotFound);
        AssertError(serviceRevisionResult.Error, RequestErrorKind.Conflict);
    }

    static Scenario CreateScenario(bool rejectCursor = false)
    {
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        return new Scenario(tenantId, programId, serviceId,
            new ProgramDirectory { RejectCursor = rejectCursor },
            new ClientServiceDirectory { RejectCursor = rejectCursor },
            new AggregateReader(ProgramSource(tenantId, programId)));
    }

    static RequestContext<T> Context<T>(T request) where T : IRequestBase =>
        new(request, new ClaimsPrincipal());

    static ProgramPlan Plan() => new(null, null, null, null, null, null);

    static ProgramView Program(Uuid tenantId, Uuid programId) => new(tenantId, programId,
        "Readiness", "readiness", "type_i", 1, Plan(), Uuid.CreateVersion4(), "Owner",
        DateTimeOffset.UtcNow, []);

    static ClientServiceView Service(Uuid tenantId, Uuid serviceId, Uuid programId) =>
        new(tenantId, serviceId, 1, "Payroll", "Run payroll", "Operations", "active",
            Uuid.CreateVersion4(), "Owner", DateTimeOffset.UtcNow, programId);

    static ComplianceProgram ProgramSource(Uuid tenantId, Uuid programId)
    {
        var program = new ComplianceProgram(tenantId, programId);
        Assert.True(program.Create("Readiness", Plan(), Uuid.CreateVersion4(), "Owner",
            DateTimeOffset.UtcNow).IsSuccess);
        return program;
    }

    static void AssertValidation(RequestError? error) =>
        AssertError(error, RequestErrorKind.Validation);

    static void AssertError(RequestError? error, RequestErrorKind kind) =>
        Assert.Equal(kind, Assert.IsType<RequestError>(error).Kind);

    sealed record Scenario(Uuid TenantId, Uuid ProgramId, Uuid ServiceId,
        ProgramDirectory Programs, ClientServiceDirectory Services, AggregateReader Reader);

    sealed class ProgramDirectory : IProgramDirectoryReader
    {
        public bool RejectCursor { get; init; }
        public Page<ProgramView> ProgramPage { get; init; } = new([], null);
        public Page<ProgramRevisionView>? RevisionPage { get; init; } = new([], null);

        public ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId,
            CancellationToken ct = default) => ValueTask.FromResult<ProgramView?>(null);

        public ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) => RejectCursor
            ? ValueTask.FromException<Page<ProgramView>>(new KvDirectoryQueryException())
            : ValueTask.FromResult(ProgramPage);

        public ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) => RejectCursor
            ? ValueTask.FromException<Page<ProgramRevisionView>?>(new KvDirectoryQueryException())
            : ValueTask.FromResult(RevisionPage);

        public ValueTask<ProgramRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid programId,
            long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<ProgramRevisionView?>(null);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class ClientServiceDirectory : IClientServiceDirectoryReader
    {
        public bool RejectCursor { get; init; }
        public Page<ClientServiceView> ServicePage { get; init; } = new([], null);
        public Page<ClientServiceView> ProgramServicePage { get; init; } = new([], null);
        public Page<ClientServiceRevisionView>? RevisionPage { get; init; } = new([], null);

        public ValueTask<ClientServiceView?> GetAsync(Uuid tenantId, Uuid serviceId,
            CancellationToken ct = default) => ValueTask.FromResult<ClientServiceView?>(null);

        public ValueTask<Page<ClientServiceView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) => RejectCursor
            ? ValueTask.FromException<Page<ClientServiceView>>(new KvDirectoryQueryException())
            : ValueTask.FromResult(ServicePage);

        public ValueTask<Page<ClientServiceView>> ListProgramAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) => RejectCursor
            ? ValueTask.FromException<Page<ClientServiceView>>(new KvDirectoryQueryException())
            : ValueTask.FromResult(ProgramServicePage);

        public ValueTask<Page<ClientServiceRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid serviceId, int limit, string? cursor, CancellationToken ct = default) => RejectCursor
            ? ValueTask.FromException<Page<ClientServiceRevisionView>?>(new KvDirectoryQueryException())
            : ValueTask.FromResult(RevisionPage);

        public ValueTask<ClientServiceRevisionView?> GetRevisionAsync(Uuid tenantId,
            Uuid serviceId, long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<ClientServiceRevisionView?>(null);
    }

    sealed class AggregateReader(ComplianceProgram program) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate is ComplianceProgram
                ? (TAggregate)(Aggregate)program
                : aggregate);
    }
}
