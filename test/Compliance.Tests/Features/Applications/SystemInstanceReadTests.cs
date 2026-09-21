using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

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
        Assert.True(source.DeclareInstance(1, instanceId, "Production", "production",
            null, null, actorId, "Manager", now).IsSuccess);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var consistency = new SystemInstanceReadConsistency(directory, new SourceReader(source));
        var get = new GetSystemInstanceHandler(directory, consistency);
        var list = new ListSystemInstancesHandler(directory, consistency);
        var exactRequest = new RequestContext<GetSystemInstance>(new GetSystemInstance(
            tenantId, applicationId, instanceId, 2), new ClaimsPrincipal());
        var listRequest = new RequestContext<ListSystemInstances>(new ListSystemInstances(
            tenantId, applicationId, MinimumApplicationRevision: 2), new ClaimsPrincipal());

        // Act
        var absentProjection = await get.HandleAsync(exactRequest, CancellationToken.None);
        var absentList = await list.HandleAsync(listRequest, CancellationToken.None);
        await ProjectAsync(directory, tenantId,
            new ApplicationDeclared(tenantId, applicationId, "Payroll", "Run payroll",
                null, actorId, "Manager", now));
        var behindParent = await get.HandleAsync(exactRequest, CancellationToken.None);
        var behindList = await list.HandleAsync(listRequest, CancellationToken.None);
        var behindWithoutAnchor = await list.HandleAsync(
            new RequestContext<ListSystemInstances>(new ListSystemInstances(tenantId,
                applicationId), new ClaimsPrincipal()), CancellationToken.None);
        await ProjectAsync(directory, tenantId,
            new SystemInstanceDeclared(tenantId, applicationId, instanceId, 2,
                "Production", "production", null, null, actorId, "Manager", now));
        var caughtUp = await get.HandleAsync(exactRequest, CancellationToken.None);
        var caughtUpList = await list.HandleAsync(listRequest, CancellationToken.None);

        // Assert
        foreach (var result in new[] { absentProjection.Error, absentList.Error,
                     behindParent.Error, behindList.Error, behindWithoutAnchor.Error })
        {
            var error = Assert.IsType<RequestError>(result);
            Assert.Equal(RequestErrorKind.Conflict, error.Kind);
            Assert.True(error.IsTransient);
        }
        Assert.Equal(instanceId, caughtUp.Value.SystemInstanceId);
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
        var consistency = new SystemInstanceReadConsistency(directory, new SourceReader(source));
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

    static async Task ProjectAsync(FitzApplicationDirectory directory, Uuid tenantId,
        DomainEvent domainEvent)
    {
        var identity = new CheckpointIdentity("ApplicationDirectory",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using var batch = await directory.BeginAsync(
            new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }
}
