using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationRelationshipTests
{
    [Fact]
    public void ShouldPersistDirectedRelationshipWithoutChangingApplicationRevisionGivenValidAdd()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var changedAt = DateTimeOffset.UtcNow;
        var source = NewApplication(tenantId, sourceId, actorId, changedAt);

        // Act
        var result = source.AddApplicationRelationship(0, Uuid.CreateVersion4(), targetId,
            "depends_on", actorId, "Inventory manager", changedAt);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("active", result.Value.State);
        Assert.Equal(1, result.Value.RelationshipRevision);
        Assert.Equal(1, source.Revision);
        Assert.Equal(1, source.SourceRelationshipRevision);
        var added = Assert.IsType<ApplicationRelationshipAdded>(
            new AggregateScenario<DeclaredApplication>(source).PendingEvents[^1]);
        Assert.Equal(tenantId, added.TenantId);
        Assert.Equal(sourceId, added.ApplicationId);
        Assert.Equal(targetId, added.TargetApplicationId);
        Assert.Equal("depends_on", added.RelationshipType);
        Assert.Equal(actorId, added.ActorMemberId);
        Assert.Equal(ActorReference.ForMember(actorId, "Inventory manager"), added.Actor);
        var relationship = Assert.Single(source.ReadApplicationRelationships());
        Assert.Equal(added.RelationshipId, relationship.RelationshipId);
        Assert.Equal(targetId, relationship.TargetApplicationId);
        Assert.Equal("depends_on", relationship.RelationshipType);
    }

    [Fact]
    public async Task ShouldRehydratePersistedRelationshipAndActorGivenSourceStreamReplay()
    {
        // Arrange
        var events = new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton(TimeProvider.System);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var tenantId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var relationshipId = Uuid.CreateVersion4();
        var changedAt = DateTimeOffset.UtcNow;
        var source = NewApplication(tenantId, sourceId, actorId, changedAt);
        await writer.SaveAsync(source, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);

        // Act
        var writable = await reader.HydrateAsync(new DeclaredApplication(tenantId, sourceId));
        var added = writable.AddApplicationRelationship(0, relationshipId, targetId,
            "depends_on", actorId, "Inventory manager", changedAt.AddMinutes(1));
        Assert.True(added.IsSuccess);
        await writer.SaveAsync(writable, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var persisted = await reader.HydrateAsync(new DeclaredApplication(tenantId, sourceId));
        var records = new List<DomainEventRecord>();
        await foreach (var record in events.ReadAsync(persisted.Stream, 0,
                           CancellationToken.None))
            records.Add(record);

        // Assert
        Assert.Equal(1, persisted.Revision);
        Assert.Equal(1, persisted.SourceRelationshipRevision);
        Assert.Equal(relationshipId, Assert.Single(persisted.ReadApplicationRelationships())
            .RelationshipId);
        var addedEvent = Assert.IsType<ApplicationRelationshipAdded>(
            Assert.Single(records, record => record.Event is ApplicationRelationshipAdded).Event);
        Assert.Equal(ActorReference.ForMember(actorId, "Inventory manager"), addedEvent.Actor);
        Assert.Equal(targetId, addedEvent.TargetApplicationId);
    }

    [Fact]
    public void ShouldRetainRemovalTombstoneAndPreventOldReplayGivenReplacementRelationship()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var oldRelationshipId = Uuid.CreateVersion4();
        var replacementRelationshipId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = NewApplication(tenantId, sourceId, actorId, now);
        Assert.True(source.AddApplicationRelationship(0, oldRelationshipId, targetId,
            "replaces", actorId, "Manager", now).IsSuccess);
        var removed = source.RemoveApplicationRelationship(1, oldRelationshipId,
            actorId, "Manager", now.AddMinutes(1));

        // Act
        var oldAddReplay = source.AddApplicationRelationship(0, oldRelationshipId,
            targetId, "replaces", actorId, "Changed display", now.AddMinutes(2));
        var oldRemoveReplay = source.RemoveApplicationRelationship(0, oldRelationshipId,
            actorId, "Changed display", now.AddMinutes(3));
        var replacement = source.AddApplicationRelationship(2, replacementRelationshipId,
            targetId, "replaces", actorId, "Manager", now.AddMinutes(4));
        var oldRemoveAfterReplacement = source.RemoveApplicationRelationship(0,
            oldRelationshipId, actorId, "Manager", now.AddMinutes(5));
        var replay = new AggregateScenario<DeclaredApplication>(
            new DeclaredApplication(tenantId, sourceId))
            .Given(new AggregateScenario<DeclaredApplication>(source).PendingEvents.ToArray())
            .Aggregate;

        // Assert
        Assert.Equal("removed", AssertSuccess(removed).State);
        Assert.Equal(2, removed.Value.RelationshipRevision);
        Assert.Equal("removed", AssertSuccess(oldAddReplay).State);
        Assert.Equal(2, oldAddReplay.Value.RelationshipRevision);
        Assert.Equal("removed", AssertSuccess(oldRemoveReplay).State);
        Assert.Equal("active", AssertSuccess(replacement).State);
        Assert.Equal("removed", AssertSuccess(oldRemoveAfterReplacement).State);
        Assert.Equal(3, source.SourceRelationshipRevision);
        Assert.Equal(1, source.Revision);
        Assert.Equal(1, replay.Revision);
        Assert.Equal(3, replay.SourceRelationshipRevision);
        var active = Assert.Single(replay.ReadApplicationRelationships());
        Assert.Equal(replacementRelationshipId, active.RelationshipId);
        Assert.Equal(targetId, active.TargetApplicationId);
        Assert.Equal("replaces", active.RelationshipType);
    }

    [Fact]
    public void ShouldEnforceTupleUniquenessAndIndependentExpectedRevisionGivenCompetingChanges()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var source = NewApplication(tenantId, sourceId, Uuid.CreateVersion4(),
            DateTimeOffset.UtcNow);
        var firstId = Uuid.CreateVersion4();
        Assert.True(source.AddApplicationRelationship(0, firstId, targetId,
            "depends_on", Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow).IsSuccess);
        var pendingBefore = new AggregateScenario<DeclaredApplication>(source)
            .PendingEvents.Count;

        // Act
        var duplicateTuple = source.AddApplicationRelationship(1, Uuid.CreateVersion4(),
            targetId, "depends_on", Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow);
        var otherType = source.AddApplicationRelationship(1, Uuid.CreateVersion4(),
            targetId, "replaces", Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow);
        var stale = source.AddApplicationRelationship(0, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), "depends_on", Uuid.CreateVersion4(), "Manager",
            DateTimeOffset.UtcNow);
        var unsupported = source.AddApplicationRelationship(source.SourceRelationshipRevision,
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), "DEPENDS_ON",
            Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow);
        var selfLink = source.AddApplicationRelationship(source.SourceRelationshipRevision,
            Uuid.CreateVersion4(), sourceId, "depends_on", Uuid.CreateVersion4(),
            "Manager", DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(duplicateTuple.Error).Kind);
        Assert.True(otherType.IsSuccess);
        var staleError = Assert.IsType<RequestError>(stale.Error);
        Assert.Equal(RequestErrorKind.Conflict, staleError.Kind);
        Assert.True(staleError.IsTransient);
        Assert.DoesNotContain("2", staleError.Message, StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(unsupported.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(selfLink.Error).Kind);
        Assert.Equal(pendingBefore + 1,
            new AggregateScenario<DeclaredApplication>(source).PendingEvents.Count);
        Assert.Equal(1, source.Revision);
        Assert.Equal(2, source.SourceRelationshipRevision);
    }

    [Fact]
    public void ShouldRejectPayloadReuseAndUnsupportedIdentityGivenRelationshipId()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var relationshipId = Uuid.CreateVersion4();
        var source = NewApplication(tenantId, sourceId, Uuid.CreateVersion4(),
            DateTimeOffset.UtcNow);
        Assert.True(source.AddApplicationRelationship(0, relationshipId, targetId,
            "depends_on", Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow).IsSuccess);

        // Act
        var payloadConflict = source.AddApplicationRelationship(0, relationshipId,
            Uuid.CreateVersion4(), "depends_on", Uuid.CreateVersion4(), "Manager",
            DateTimeOffset.UtcNow);
        var emptyRelationshipId = source.AddApplicationRelationship(1, Uuid.Empty,
            Uuid.CreateVersion4(), "depends_on", Uuid.CreateVersion4(), "Manager",
            DateTimeOffset.UtcNow);
        var emptyTargetId = source.AddApplicationRelationship(1, Uuid.CreateVersion4(),
            Uuid.Empty, "depends_on", Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(payloadConflict.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(emptyRelationshipId.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(emptyTargetId.Error).Kind);
        Assert.Equal(1, source.SourceRelationshipRevision);
    }

    static DeclaredApplication NewApplication(Uuid tenantId, Uuid applicationId,
        Uuid actorId, DateTimeOffset changedAt)
    {
        var application = new DeclaredApplication(tenantId, applicationId);
        Assert.True(application.Declare("Payroll", "Run payroll", null, actorId,
            "Inventory manager", changedAt).IsSuccess);
        return application;
    }

    static ApplicationRelationshipChange AssertSuccess(
        Result<ApplicationRelationshipChange> result)
    {
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
