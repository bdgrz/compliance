using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationListContractTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ShouldRejectOutOfRangeLimitGivenApplicationLists(int limit)
    {
        // Arrange
        var scenario = await CreateScenarioAsync();
        var applications = new ListApplicationsHandler(scenario.Directory);
        var history = new ListApplicationRevisionsHandler(scenario.Directory,
            new ApplicationHistoryReadConsistency(scenario.Directory,
                new SourceReader(scenario.Source)));
        var instances = new ListSystemInstancesHandler(scenario.Directory,
            new SystemInstanceReadConsistency(scenario.Directory,
                new SourceReader(scenario.Source)));

        // Act
        var applicationResult = await applications.HandleAsync(Context(new ListApplications(
            scenario.TenantId, limit)), CancellationToken.None);
        var historyResult = await history.HandleAsync(Context(new ListApplicationRevisions(
            scenario.TenantId, scenario.ApplicationId, limit)), CancellationToken.None);
        var instanceResult = await instances.HandleAsync(Context(new ListSystemInstances(
            scenario.TenantId, scenario.ApplicationId, limit)), CancellationToken.None);

        // Assert
        AssertValidation(applicationResult.Error);
        AssertValidation(historyResult.Error);
        AssertValidation(instanceResult.Error);
    }

    [Fact]
    public async Task ShouldRejectMalformedCursorGivenApplicationLists()
    {
        // Arrange
        var scenario = await CreateScenarioAsync();
        var applications = new ListApplicationsHandler(scenario.Directory);
        var history = new ListApplicationRevisionsHandler(scenario.Directory,
            new ApplicationHistoryReadConsistency(scenario.Directory,
                new SourceReader(scenario.Source)));
        var instances = new ListSystemInstancesHandler(scenario.Directory,
            new SystemInstanceReadConsistency(scenario.Directory,
                new SourceReader(scenario.Source)));

        // Act
        var applicationResult = await applications.HandleAsync(Context(new ListApplications(
            scenario.TenantId, Cursor: "not-a-cursor")), CancellationToken.None);
        var historyResult = await history.HandleAsync(Context(new ListApplicationRevisions(
            scenario.TenantId, scenario.ApplicationId, Cursor: "not-a-cursor")),
            CancellationToken.None);
        var instanceResult = await instances.HandleAsync(Context(new ListSystemInstances(
            scenario.TenantId, scenario.ApplicationId, Cursor: "not-a-cursor")),
            CancellationToken.None);

        // Assert
        AssertValidation(applicationResult.Error);
        AssertValidation(historyResult.Error);
        AssertValidation(instanceResult.Error);
    }

    static RequestContext<T> Context<T>(T request) where T : IRequestBase =>
        new(request, new ClaimsPrincipal());

    static void AssertValidation(RequestError? error) =>
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(error).Kind);

    static async Task<Scenario> CreateScenarioAsync()
    {
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        Assert.True(source.DeclareInstance(1, instanceId, "Production", "production",
            null, "payroll-prod", actorId, "Manager", now).IsSuccess);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("ApplicationDirectory",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using var batch = await directory.BeginAsync(
            new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
            "Payroll", "Run payroll", null, actorId, "Manager", now));
        await directory.ApplyAsync(new SystemInstanceDeclared(tenantId, applicationId, instanceId,
            2, "Production", "production", null, "payroll-prod", actorId, "Manager", now));
        await batch.CommitAsync(ProjectionCheckpoint.Start);
        return new Scenario(tenantId, applicationId, source, directory);
    }

    sealed record Scenario(Uuid TenantId, Uuid ApplicationId, DeclaredApplication Source,
        FitzApplicationDirectory Directory);

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }
}
