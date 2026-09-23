using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class SystemInstanceCutoverTests
{
    [Fact]
    public async Task ShouldReuseLegacyIdentityGivenRetriedDeclarationAfterCutover()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var context = Context(tenantId, applicationId);
        var instanceId = context.RequestId;
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = Application(tenantId, applicationId, actorId, now);
        var events = new InMemoryEventStore();
        await AppendLegacyAsync(events, tenantId, applicationId, instanceId, actorId, now);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var executor = new RejectingExecutor();
        var handler = Handler(executor, source, directory, events);

        // Act
        var pending = await handler.HandleAsync(context, CancellationToken.None);
        await ProjectLegacyAsync(directory, events, tenantId, applicationId);
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(pending.Error).Kind);
        Assert.True(pending.Error!.IsTransient);
        Assert.True(result.IsSuccess);
        Assert.Equal(instanceId, result.Value.SystemInstanceId);
        Assert.False(executor.Called);
    }

    [Fact]
    public async Task ShouldFenceUnprojectedLegacyIdentityGivenDifferentParentAtCutover()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var otherApplicationId = Uuid.CreateVersion4();
        var context = Context(tenantId, applicationId);
        var instanceId = context.RequestId;
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = Application(tenantId, applicationId, actorId, now);
        var events = new InMemoryEventStore();
        await AppendLegacyAsync(events, tenantId, otherApplicationId, instanceId,
            actorId, now);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var executor = new RejectingExecutor();
        var handler = Handler(executor, source, directory, events);

        // Act
        var pending = await handler.HandleAsync(context, CancellationToken.None);
        await ProjectLegacyAsync(directory, events, tenantId, otherApplicationId);
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(Assert.IsType<RequestError>(pending.Error).IsTransient);
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.False(error.IsTransient);
        Assert.False(executor.Called);
    }

    static DeclaredApplication Application(Uuid tenantId, Uuid applicationId,
        Uuid actorId, DateTimeOffset now)
    {
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null,
            actorId, "Manager", now).IsSuccess);
        return source;
    }

    static async Task AppendLegacyAsync(InMemoryEventStore events, Uuid tenantId,
        Uuid applicationId, Uuid instanceId, Uuid actorId, DateTimeOffset now)
    {
        var stream = new EventStreamAddress(tenantId.ToString(), "applications",
            applicationId.ToString());
        await events.AppendAsync(stream, 0,
        [
            DomainEventSeed.Attach(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", null, actorId, "Manager", now),
                applicationId, 1),
            DomainEventSeed.Attach(new SystemInstanceDeclared(tenantId, applicationId,
                instanceId, 2, "Production", "production", null, "payroll-prod",
                actorId, "Manager", now), applicationId, 2),
        ]);
    }

    static async Task ProjectLegacyAsync(FitzApplicationDirectory directory,
        InMemoryEventStore events, Uuid tenantId, Uuid applicationId)
    {
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            identity, ProjectionCheckpoint.Start));
        await foreach (var record in events.ReadAsync(new EventStreamAddress(
                           tenantId.ToString(), "applications", applicationId.ToString()),
                           0, CancellationToken.None))
            await directory.ApplyAsync(record.Event);
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(
                           tenantId.ToString()), cursor, CancellationToken.None))
            cursor = record.NextCursor;
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }

    static DeclareSystemInstanceHandler Handler(RejectingExecutor executor,
        DeclaredApplication source, FitzApplicationDirectory directory,
        InMemoryEventStore events) =>
        new(executor, new SourceReader(source), directory, events, TimeProvider.System);

    static RequestContext<DeclareSystemInstance> Context(Uuid tenantId,
        Uuid applicationId)
    {
        var actorId = Uuid.CreateVersion4();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", actorId.ToString())], "test"));
        return new RequestContext<DeclareSystemInstance>(new DeclareSystemInstance(
            tenantId, applicationId, 1, "Production", "production", null,
            "payroll-prod"), actor);
    }

    sealed class SourceReader(DeclaredApplication source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate is DeclaredApplication && aggregate.Id == source.Id
                ? (TAggregate)(Aggregate)source : aggregate);
    }

    sealed class RejectingExecutor : IAggregateExecutor
    {
        public bool Called { get; private set; }

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            Called = true;
            throw new InvalidOperationException("The instance stream must not be written.");
        }

        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            Called = true;
            throw new InvalidOperationException("The instance stream must not be written.");
        }
    }
}
