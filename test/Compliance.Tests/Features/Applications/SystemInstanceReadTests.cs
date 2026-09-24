using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class SystemInstanceReadTests
{
    [Fact]
    public async Task ShouldReportTransientLagUntilParentAndInstanceProjectGivenDeclaration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null,
            actorId, "Manager", now).IsSuccess);
        var instance = new DeclaredSystemInstance(tenantId, instanceId);
        Assert.True(instance.Declare(applicationId, "Production", "production",
            null, null, actorId, "Manager", now).IsSuccess);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var events = new InMemoryEventStore();
        var consistency = new SystemInstanceReadConsistency(directory,
            new SourceReader(source, instance), new LegacySystemInstanceSource(directory, events), events);
        var get = new GetSystemInstanceHandler(directory, consistency);
        var list = new ListSystemInstancesHandler(directory, consistency);
        var exactRequest = new RequestContext<GetSystemInstance>(new GetSystemInstance(
            tenantId, applicationId, instanceId, 1, 1), new ClaimsPrincipal());
        var listRequest = new RequestContext<ListSystemInstances>(new ListSystemInstances(
            tenantId, applicationId, MinimumApplicationRevision: 1), new ClaimsPrincipal());

        // Act
        var absentProjection = await get.HandleAsync(exactRequest, CancellationToken.None);
        var absentList = await list.HandleAsync(listRequest, CancellationToken.None);
        await ProjectAsync(directory, tenantId,
            new ApplicationDeclared(tenantId, applicationId, "Payroll", "Run payroll",
                null, actorId, "Manager", now));
        var behindParent = await get.HandleAsync(exactRequest, CancellationToken.None);
        await ProjectAsync(directory, tenantId,
            new SystemInstanceRegistered(tenantId, applicationId, instanceId, 1,
                "Production", "production", null, null, actorId, "Manager", now));
        var caughtUp = await get.HandleAsync(exactRequest, CancellationToken.None);
        var caughtUpList = await list.HandleAsync(listRequest, CancellationToken.None);

        // Assert
        foreach (var result in new[] { absentProjection.Error, absentList.Error,
                     behindParent.Error })
        {
            var error = Assert.IsType<RequestError>(result);
            Assert.Equal(RequestErrorKind.Conflict, error.Kind);
            Assert.True(error.IsTransient);
        }
        Assert.Equal(instanceId, caughtUp.Value.SystemInstanceId);
        Assert.Equal(1, caughtUp.Value.Revision);
        Assert.Equal(instanceId, Assert.Single(caughtUpList.Value.Items).SystemInstanceId);
    }

    [Fact]
    public async Task ShouldDistinguishInvalidFutureAndEmptyListGivenSourceApplication()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null,
            actorId, "Manager", now).IsSuccess);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var events = new InMemoryEventStore();
        var consistency = new SystemInstanceReadConsistency(directory,
            new SourceReader(source), new LegacySystemInstanceSource(directory, events), events);
        var list = new ListSystemInstancesHandler(directory, consistency);
        await ProjectAsync(directory, tenantId,
            new ApplicationDeclared(tenantId, applicationId, "Payroll", "Run payroll",
                null, actorId, "Manager", now));

        // Act
        var empty = await list.HandleAsync(new RequestContext<ListSystemInstances>(
            new ListSystemInstances(tenantId, applicationId, MinimumApplicationRevision: 1),
            new ClaimsPrincipal()), CancellationToken.None);
        var future = await list.HandleAsync(new RequestContext<ListSystemInstances>(
            new ListSystemInstances(tenantId, applicationId, MinimumApplicationRevision: 2),
            new ClaimsPrincipal()), CancellationToken.None);
        var invalid = await list.HandleAsync(new RequestContext<ListSystemInstances>(
            new ListSystemInstances(tenantId, applicationId, MinimumApplicationRevision: 0),
            new ClaimsPrincipal()), CancellationToken.None);
        var absentInstance = await new GetSystemInstanceHandler(directory, consistency).HandleAsync(
            new RequestContext<GetSystemInstance>(new GetSystemInstance(tenantId,
                applicationId, Uuid.CreateVersion4(), 1), new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        Assert.Empty(empty.Value.Items);
        var futureError = Assert.IsType<RequestError>(future.Error);
        Assert.Equal(RequestErrorKind.Conflict, futureError.Kind);
        Assert.True(futureError.IsTransient);
        Assert.Contains("source", futureError.Message, StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(invalid.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(absentInstance.Error).Kind);
    }

    [Fact]
    public async Task ShouldWaitForInstanceAreaGivenListProjectionBehindSource()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null,
            actorId, "Manager", now).IsSuccess);
        var events = new InMemoryEventStore();
        var appEvent = DomainEventSeed.Attach(new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now),
            applicationId, 1);
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(),
            "applications", applicationId.ToString()), 0, [appEvent]);
        var appCheckpoint = await CheckpointAfterAsync(events, tenantId);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        await ProjectAtAsync(directory, tenantId, appEvent,
            ProjectionCheckpoint.Start, appCheckpoint);
        var instanceEvent = DomainEventSeed.Attach(new SystemInstanceRegistered(
            tenantId, applicationId, instanceId, 1, "Production", "production",
            null, null, actorId, "Manager", now), instanceId, 1);
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(),
            "system-instances", instanceId.ToString()), 0, [instanceEvent]);
        var instanceCheckpoint = await CheckpointAfterAsync(events, tenantId);
        var handler = new ListSystemInstancesHandler(directory,
            new SystemInstanceReadConsistency(directory, new SourceReader(source),
                new LegacySystemInstanceSource(directory, events), events));
        var request = new RequestContext<ListSystemInstances>(new ListSystemInstances(
            tenantId, applicationId, MinimumApplicationRevision: 1), new ClaimsPrincipal());

        // Act
        var lagging = await handler.HandleAsync(request, CancellationToken.None);
        await ProjectAtAsync(directory, tenantId, instanceEvent,
            appCheckpoint, instanceCheckpoint);
        var caughtUp = await handler.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(lagging.Error).Kind);
        Assert.True(lagging.Error!.IsTransient);
        Assert.Equal(instanceId, Assert.Single(caughtUp.Value.Items).SystemInstanceId);
    }

    static async Task ProjectAsync(FitzApplicationDirectory directory, Uuid tenantId,
        DomainEvent domainEvent)
    {
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using var batch = await directory.BeginAsync(
            new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static async Task ProjectAtAsync(FitzApplicationDirectory directory, Uuid tenantId,
        DomainEvent domainEvent, ProjectionCheckpoint before, ProjectionCheckpoint after)
    {
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            identity, before));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(after);
    }

    static async Task<ProjectionCheckpoint> CheckpointAfterAsync(InMemoryEventStore events,
        Uuid tenantId)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(
                           tenantId.ToString()), cursor, CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed class SourceReader(DeclaredApplication application,
        DeclaredSystemInstance? instance = null) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate is DeclaredApplication
                ? (TAggregate)(Aggregate)application
                : instance is not null && aggregate.Id == instance.Id
                    ? (TAggregate)(Aggregate)instance
                    : aggregate);
    }
}
