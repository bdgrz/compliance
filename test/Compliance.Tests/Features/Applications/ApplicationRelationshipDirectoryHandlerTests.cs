using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationRelationshipDirectoryHandlerTests
{
    [Fact]
    public async Task ShouldIndexAndUpdateDirectedRelationshipGivenLifecycleEvents()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var relationshipId = ApplicationRelationshipIdentityForTest(sourceId, "replaces", targetId);
        var directory = new FitzApplicationRelationshipDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("ApplicationRelationshipsV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "applications"));
        var now = DateTimeOffset.UtcNow;
        var recorded = new ApplicationRelationshipRecorded(tenantId, relationshipId,
            sourceId, targetId, "replaces", 1, "proposed", 2, 3,
            Uuid.CreateVersion4(), "Author", now);

        await ApplyAsync(directory, identity, recorded);
        await ApplyAsync(directory, identity, recorded);
        var outgoing = await directory.ListAsync(tenantId, sourceId, "outgoing", 20, null);
        var incoming = await directory.ListAsync(tenantId, targetId, "incoming", 20, null);

        // Act
        var approved = new ApplicationRelationshipApproved(tenantId, relationshipId,
            sourceId, targetId, 2, 2, 3, new string('a', 64), Uuid.CreateVersion4(),
            "Approver", now.AddMinutes(1));
        await ApplyAsync(directory, identity, approved);
        var afterApproval = await directory.ListAsync(tenantId, sourceId, "outgoing", 20, null);
        var removed = new ApplicationRelationshipRemoved(tenantId, relationshipId,
            sourceId, targetId, "replaces", 3, "No longer needed", Uuid.CreateVersion4(),
            "Remover", now.AddMinutes(2));
        await ApplyAsync(directory, identity, removed);
        var afterRemoval = await directory.ListAsync(tenantId, sourceId, "outgoing", 20, null);
        var isolated = await directory.ListAsync(Uuid.CreateVersion4(), sourceId,
            "outgoing", 20, null);

        // Assert
        Assert.Equal(relationshipId, Assert.Single(outgoing.Items).RelationshipId);
        Assert.Equal(relationshipId, Assert.Single(incoming.Items).RelationshipId);
        Assert.Equal("approved", Assert.Single(afterApproval.Items).Status);
        Assert.Equal("removed", Assert.Single(afterRemoval.Items).Status);
        Assert.Equal("No longer needed", afterRemoval.Items[0].RemovalReason);
        Assert.Empty(isolated.Items);
    }

    [Fact]
    public async Task ShouldPageOutgoingRelationshipsGivenMultipleTargets()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var firstTargetId = Uuid.CreateVersion4();
        var secondTargetId = Uuid.CreateVersion4();
        var directory = new FitzApplicationRelationshipDirectory(new InMemoryKvClient());
        var identity = Identity(tenantId);
        var now = DateTimeOffset.UtcNow;
        await ApplyAsync(directory, identity, Recorded(tenantId, sourceId, firstTargetId, now));
        await ApplyAsync(directory, identity, Recorded(tenantId, sourceId, secondTargetId,
            now.AddMinutes(1)));

        // Act
        var firstPage = await directory.ListAsync(tenantId, sourceId, "outgoing", 1, null);
        var secondPage = await directory.ListAsync(tenantId, sourceId, "outgoing", 1,
            firstPage.NextCursor);

        // Assert
        Assert.Single(firstPage.Items);
        Assert.NotNull(firstPage.NextCursor);
        Assert.Single(secondPage.Items);
        Assert.Null(secondPage.NextCursor);
        Assert.NotEqual(firstPage.Items[0].RelationshipId,
            secondPage.Items[0].RelationshipId);
    }

    [Fact]
    public void ShouldReturnStableRegistrationGivenRelationshipCommandReplay()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var application = new DeclaredApplication(tenantId, sourceId);
        Assert.True(application.Declare("Payroll", "Run payroll", null, actorId,
            "Manager", now).IsSuccess);

        // Act
        var first = application.RecordRelationship(targetId, 1, 1, "depends_on",
            actorId, "Manager", now.AddMinutes(1));
        var replay = application.RecordRelationship(targetId, 1, 1, "depends_on",
            actorId, "Manager", now.AddMinutes(2));
        var relationshipEvents = new AggregateScenario<DeclaredApplication>(application)
            .PendingEvents.OfType<ApplicationRelationshipRecorded>().ToArray();

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value, replay.Value);
        Assert.Single(relationshipEvents);
    }

    [Fact]
    public async Task ShouldRecordRemoveAndListGivenActiveVisibleApplications()
    {
        // Arrange
        var services = new ServiceCollection();
        var eventStore = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(eventStore);
        services.AddSingleton(TimeProvider.System);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider;
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var actor = Actor(userId);
        var actorMemberId = RbacIds.Member(tenantId, userId);
        var sourceId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var writer = service.GetRequiredService<IAggregateWriter>();
        var reader = service.GetRequiredService<IAggregateReader>();
        var executor = service.GetRequiredService<IAggregateExecutor>();
        await SaveApplicationAsync(writer, tenantId, sourceId, actorMemberId, now);
        await SaveApplicationAsync(writer, tenantId, targetId, actorMemberId, now);
        var visibility = RestrictedApplicationVisibilityFixture.Create(reader,
            organizationPermission: true);
        var recordHandler = new RecordApplicationRelationshipHandler(executor, reader,
            visibility, TimeProvider.System);
        var removeHandler = new RemoveApplicationRelationshipHandler(executor, reader,
            visibility, TimeProvider.System);
        var request = new RecordApplicationRelationship(tenantId, sourceId, targetId,
            "depends_on");
        var recordContext = new RequestContext<RecordApplicationRelationship>(request, actor);

        // Act
        var recorded = await recordHandler.HandleAsync(recordContext, CancellationToken.None);
        var replayed = await recordHandler.HandleAsync(recordContext, CancellationToken.None);
        var sourceEvents = await ReadApplicationEventsAsync(eventStore, tenantId, sourceId);
        var relationshipEvent = Assert.IsType<ApplicationRelationshipRecorded>(
            Assert.Single(sourceEvents, item => item is ApplicationRelationshipRecorded));
        var directory = new FitzApplicationRelationshipDirectory(new InMemoryKvClient());
        await ApplyAsync(directory, Identity(tenantId), relationshipEvent);
        var listHandler = new ListApplicationRelationshipsHandler(reader, directory,
            new ApplicationRelationshipReadConsistency(directory, new InMemoryEventStore()),
            visibility);
        var beforeRemoval = await listHandler.HandleAsync(
            new RequestContext<ListApplicationRelationships>(new ListApplicationRelationships(
                tenantId, sourceId, "outgoing"), actor), CancellationToken.None);
        var removed = await removeHandler.HandleAsync(
            new RequestContext<RemoveApplicationRelationship>(new RemoveApplicationRelationship(
                tenantId, sourceId, targetId, "depends_on", recorded.Value.Revision,
                "No longer required"), actor), CancellationToken.None);
        var sourceEventsAfterRemoval = await ReadApplicationEventsAsync(eventStore, tenantId,
            sourceId);
        var removalEvent = Assert.IsType<ApplicationRelationshipRemoved>(
            Assert.Single(sourceEventsAfterRemoval, item => item is ApplicationRelationshipRemoved));
        await ApplyAsync(directory, Identity(tenantId), removalEvent);
        var afterRemoval = await listHandler.HandleAsync(
            new RequestContext<ListApplicationRelationships>(new ListApplicationRelationships(
                tenantId, sourceId, "outgoing"), actor), CancellationToken.None);

        // Assert
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        Assert.Equal(recorded.Value, replayed.Value);
        Assert.True(beforeRemoval.IsSuccess, beforeRemoval.Error?.Message);
        Assert.Equal("active", Assert.Single(beforeRemoval.Value.Items).Status);
        Assert.True(removed.IsSuccess, removed.Error?.Message);
        Assert.True(afterRemoval.IsSuccess, afterRemoval.Error?.Message);
        Assert.Equal("removed", Assert.Single(afterRemoval.Value.Items).Status);
    }

    [Fact]
    public async Task ShouldReportLagAndRecoverGivenRelationshipProjectionCatchup()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var relationshipId = ApplicationRelationshipIdentityForTest(sourceId,
            "depends_on", targetId);
        var directory = new FitzApplicationRelationshipDirectory(new InMemoryKvClient());
        var events = new InMemoryEventStore();
        var consistency = new ApplicationRelationshipReadConsistency(directory, events);
        var stream = new EventStreamAddress(tenantId.ToString(), "applications",
            sourceId.ToString());
        var recorded = new ApplicationRelationshipRecorded(tenantId, relationshipId,
            sourceId, targetId, "depends_on", 1, "active", 1, 1,
            Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow);
        recorded.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), sourceId,
            1, DateTimeOffset.UtcNow));

        // Act
        var caughtUpInitially = await consistency.EnsureCaughtUpAsync(tenantId,
            CancellationToken.None);
        await events.AppendAsync(stream, 0, [recorded]);
        var lagged = await consistency.EnsureCaughtUpAsync(tenantId,
            CancellationToken.None);
        await using var source = events.ReadAsync(
            EventStreamPattern.ForPattern(tenantId.ToString(), "applications"),
            EventCursor.Start, CancellationToken.None).GetAsyncEnumerator();
        Assert.True(await source.MoveNextAsync());
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(source.Current.Event);
            await batch.CommitAsync(new ProjectionCheckpoint(source.Current.NextCursor));
        }
        var recovered = await consistency.EnsureCaughtUpAsync(tenantId,
            CancellationToken.None);
        var rows = await directory.ListAsync(tenantId, sourceId, "outgoing", 20, null);

        // Assert
        Assert.True(caughtUpInitially.IsSuccess);
        Assert.False(lagged.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, lagged.Error.Kind);
        Assert.True(lagged.Error.IsTransient);
        Assert.True(recovered.IsSuccess);
        Assert.Equal(relationshipId, Assert.Single(rows.Items).RelationshipId);
    }

    [Fact]
    public async Task ShouldHideRestrictedCounterpartGivenVisibleApplicationRelationshipList()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var actorMemberId = RbacIds.Member(tenantId, Uuid.CreateVersion4());
        var sourceId = Uuid.CreateVersion4();
        var restrictedTargetId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = Application(tenantId, sourceId, actorMemberId, false, now);
        var target = Application(tenantId, restrictedTargetId, actorMemberId, true, now);
        var reader = new SourceMapReader(source, target);
        var directory = new FitzApplicationRelationshipDirectory(new InMemoryKvClient());
        var identity = Identity(tenantId);
        await ApplyAsync(directory, identity, Recorded(tenantId, sourceId,
            restrictedTargetId, now));
        var events = new InMemoryEventStore();
        var handler = new ListApplicationRelationshipsHandler(reader, directory,
            new ApplicationRelationshipReadConsistency(directory, events),
            RestrictedApplicationVisibilityFixture.Create(reader));

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListApplicationRelationships>(
            new ListApplicationRelationships(tenantId, sourceId, "outgoing"), Actor(userId)),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Items);
        Assert.Null(result.Value.NextCursor);
    }

    static Uuid ApplicationRelationshipIdentityForTest(Uuid sourceId, string type,
        Uuid targetId) => Uuid.CreateVersion5(sourceId,
        $"application-relationship:{type}:{targetId}");

    static ApplicationRelationshipRecorded Recorded(Uuid tenantId, Uuid sourceId,
        Uuid targetId, DateTimeOffset now) => new(tenantId,
        ApplicationRelationshipIdentityForTest(sourceId, "depends_on", targetId),
        sourceId, targetId, "depends_on", 1, "active", 1, 1,
        Uuid.CreateVersion4(), "Author", now);

    static DeclaredApplication Application(Uuid tenantId, Uuid applicationId,
        Uuid actorMemberId, bool restricted, DateTimeOffset now)
    {
        var application = new DeclaredApplication(tenantId, applicationId);
        Assert.True(application.Declare("Application", "Owned system", null,
            actorMemberId, "Owner", now, isRestricted: restricted).IsSuccess);
        return application;
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "test"));

    static async Task SaveApplicationAsync(IAggregateWriter writer, Uuid tenantId,
        Uuid applicationId, Uuid actorMemberId, DateTimeOffset now)
    {
        var application = new DeclaredApplication(tenantId, applicationId);
        Assert.True(application.Declare("Application", "Owned system", null,
            actorMemberId, "Owner", now).IsSuccess);
        await writer.SaveAsync(application,
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    static async Task<DomainEvent[]> ReadApplicationEventsAsync(InMemoryEventStore events,
        Uuid tenantId, Uuid applicationId)
    {
        var result = new List<DomainEvent>();
        await foreach (var record in events.ReadAsync(new EventStreamAddress(
                           tenantId.ToString(), "applications", applicationId.ToString()),
                       0, CancellationToken.None))
            result.Add(record.Event);
        return [.. result];
    }

    static CheckpointIdentity Identity(Uuid tenantId) => new("ApplicationRelationshipsV1",
        EventStreamPattern.ForPattern(tenantId.ToString(), "applications"));

    static async Task ApplyAsync(FitzApplicationRelationshipDirectory directory,
        CheckpointIdentity identity, DomainEvent domainEvent)
    {
        await using var batch = await directory.BeginAsync(
            new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    sealed class SourceMapReader(params DeclaredApplication[] applications) : IAggregateReader
    {
        readonly Dictionary<Uuid, DeclaredApplication> _applications = applications
            .ToDictionary(static application => application.Id);

        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate is DeclaredApplication application &&
                                 _applications.TryGetValue(application.Id, out var current)
                ? (TAggregate)(Aggregate)current
                : aggregate);
    }
}
