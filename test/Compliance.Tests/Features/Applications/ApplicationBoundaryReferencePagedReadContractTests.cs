using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationBoundaryReferencePagedReadContractTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ShouldRejectOutOfRangeLimitBeforeDirectoryReadGivenBoundaryReferenceLists(
        int limit)
    {
        // Arrange
        var scenario = CreateScenario();
        var application = new ListApplicationBoundaryReferencesHandler(scenario.Reader,
            scenario.Directory, scenario.Consistency);
        var instance = new ListSystemInstanceBoundaryReferencesHandler(scenario.Reader,
            scenario.Directory, scenario.Consistency);

        // Act
        var applicationResult = await application.HandleAsync(Context(
            new ListApplicationBoundaryReferences(scenario.TenantId, scenario.ApplicationId, limit)),
            CancellationToken.None);
        var instanceResult = await instance.HandleAsync(Context(
            new ListSystemInstanceBoundaryReferences(scenario.TenantId, scenario.ApplicationId,
                scenario.SystemInstanceId, limit)), CancellationToken.None);

        // Assert
        AssertValidation(applicationResult.Error);
        AssertValidation(instanceResult.Error);
        Assert.Equal(0, scenario.Directory.ListCalls);
    }

    [Fact]
    public async Task ShouldUseDefaultPageLimitGivenBoundaryReferenceLists()
    {
        // Arrange
        var scenario = CreateScenario();
        var application = new ListApplicationBoundaryReferencesHandler(scenario.Reader,
            scenario.Directory, scenario.Consistency);
        var instance = new ListSystemInstanceBoundaryReferencesHandler(scenario.Reader,
            scenario.Directory, scenario.Consistency);

        // Act
        var applicationResult = await application.HandleAsync(Context(
            new ListApplicationBoundaryReferences(scenario.TenantId, scenario.ApplicationId)),
            CancellationToken.None);
        var instanceResult = await instance.HandleAsync(Context(
            new ListSystemInstanceBoundaryReferences(scenario.TenantId, scenario.ApplicationId,
                scenario.SystemInstanceId)), CancellationToken.None);

        // Assert
        Assert.True(applicationResult.IsSuccess);
        Assert.True(instanceResult.IsSuccess);
        Assert.Equal([50, 50], scenario.Directory.Limits);
    }

    [Fact]
    public async Task ShouldRejectInvalidCursorGivenBoundaryReferenceLists()
    {
        // Arrange
        var scenario = CreateScenario(rejectCursor: true);
        var application = new ListApplicationBoundaryReferencesHandler(scenario.Reader,
            scenario.Directory, scenario.Consistency);
        var instance = new ListSystemInstanceBoundaryReferencesHandler(scenario.Reader,
            scenario.Directory, scenario.Consistency);

        // Act
        var applicationResult = await application.HandleAsync(Context(
            new ListApplicationBoundaryReferences(scenario.TenantId, scenario.ApplicationId,
                Cursor: "not-a-cursor")), CancellationToken.None);
        var instanceResult = await instance.HandleAsync(Context(
            new ListSystemInstanceBoundaryReferences(scenario.TenantId, scenario.ApplicationId,
                scenario.SystemInstanceId, Cursor: "not-a-cursor")), CancellationToken.None);

        // Assert
        AssertValidation(applicationResult.Error);
        AssertValidation(instanceResult.Error);
    }

    [Fact]
    public async Task ShouldHideMismatchedProjectionRowsGivenBoundaryReferenceLists()
    {
        // Arrange
        var scenario = CreateScenario();
        var application = new ListApplicationBoundaryReferencesHandler(scenario.Reader,
            scenario.Directory, scenario.Consistency);
        var instance = new ListSystemInstanceBoundaryReferencesHandler(scenario.Reader,
            scenario.Directory, scenario.Consistency);
        scenario.Directory.Page = new Page<ApplicationBoundaryReferenceView>([
            Reference(Uuid.CreateVersion4(), "application", scenario.ApplicationId),
        ], null);

        // Act
        var applicationResult = await application.HandleAsync(Context(
            new ListApplicationBoundaryReferences(scenario.TenantId, scenario.ApplicationId)),
            CancellationToken.None);
        scenario.Directory.Page = new Page<ApplicationBoundaryReferenceView>([
            Reference(scenario.TenantId, "system_instance", Uuid.CreateVersion4()),
        ], null);
        var instanceResult = await instance.HandleAsync(Context(
            new ListSystemInstanceBoundaryReferences(scenario.TenantId, scenario.ApplicationId,
                scenario.SystemInstanceId)), CancellationToken.None);

        // Assert
        AssertNotFound(applicationResult.Error);
        AssertNotFound(instanceResult.Error);
    }

    [Fact]
    public async Task ShouldHideSystemInstanceGivenWrongApplicationParent()
    {
        // Arrange
        var scenario = CreateScenario();
        var handler = new ListSystemInstanceBoundaryReferencesHandler(scenario.Reader,
            scenario.Directory, scenario.Consistency);

        // Act
        var result = await handler.HandleAsync(Context(new ListSystemInstanceBoundaryReferences(
            scenario.TenantId, Uuid.CreateVersion4(), scenario.SystemInstanceId)),
            CancellationToken.None);

        // Assert
        AssertNotFound(result.Error);
        Assert.Equal(0, scenario.Directory.ListCalls);
    }

    static RequestContext<T> Context<T>(T request) where T : IRequestBase =>
        new(request, new ClaimsPrincipal());

    static void AssertValidation(RequestError? error) =>
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(error).Kind);

    static void AssertNotFound(RequestError? error) =>
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(error).Kind);

    static Scenario CreateScenario(bool rejectCursor = false)
    {
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager",
            DateTimeOffset.UtcNow).IsSuccess);
        Assert.True(source.DeclareInstance(1, systemInstanceId, "Production", "production", null,
            "payroll-prod", actorId, "Manager", DateTimeOffset.UtcNow).IsSuccess);
        var directory = new Directory { RejectCursor = rejectCursor };
        return new Scenario(tenantId, applicationId, systemInstanceId, directory,
            new SourceReader(source), new ApplicationBoundaryReferenceReadConsistency(directory,
                new InMemoryEventStore()));
    }

    static ApplicationBoundaryReferenceView Reference(Uuid tenantId, string subjectType,
        Uuid governedRecordId) => new(tenantId, subjectType, governedRecordId,
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
        1, "draft", null, "inclusion", "Payroll", "Operations", "In scope");

    sealed record Scenario(Uuid TenantId, Uuid ApplicationId, Uuid SystemInstanceId,
        Directory Directory, SourceReader Reader, ApplicationBoundaryReferenceReadConsistency Consistency);

    sealed class Directory : IApplicationBoundaryReferenceDirectory
    {
        public bool RejectCursor { get; init; }
        public int ListCalls { get; private set; }
        public List<int> Limits { get; } = [];
        public Page<ApplicationBoundaryReferenceView> Page { get; set; } = new([], null);

        public ValueTask<Page<ApplicationBoundaryReferenceView>> ListAsync(Uuid tenantId,
            string subjectType, Uuid recordId, int limit, string? cursor,
            CancellationToken ct = default)
        {
            ListCalls++;
            Limits.Add(limit);
            return RejectCursor
                ? ValueTask.FromException<Page<ApplicationBoundaryReferenceView>>(
                    new KvDirectoryQueryException())
                : ValueTask.FromResult(Page);
        }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class SourceReader(DeclaredApplication source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate.Id == source.Id ? (TAggregate)(Aggregate)source : aggregate);
    }
}
