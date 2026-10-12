using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationRelationshipRequestTests
{
    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("list")]
    public async Task ShouldRequireInventoryManageGivenRelationshipRequest(string operation)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        IApplicationInventoryRequest request = operation switch
        {
            "add" => new AddApplicationRelationship(tenantId, applicationId,
                Uuid.CreateVersion4(), 0, Uuid.CreateVersion4(), "depends_on"),
            "remove" => new RemoveApplicationRelationship(tenantId, applicationId,
                Uuid.CreateVersion4(), 0),
            _ => new ListApplicationRelationships(tenantId, applicationId),
        };
        var permissions = new RecordingPermissionAuthorizer(allowed: false);
        var scopedPermissions = new ScopedReadOnlyPermissions();
        var authorizer = new ApplicationInventoryAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), permissions, scopedPermissions);
        var context = new RequestContext<IApplicationInventoryRequest>(request,
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())],
                "BdgrzSession")));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal([RbacPermissions.ApplicationInventoryManage], permissions.Permissions);
        Assert.Equal(0, scopedPermissions.Checks);
    }

    [Fact]
    public async Task ShouldPersistAttributedAddAndRemovalGivenInventoryManagerRequests()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var targetId = Uuid.CreateVersion4();
        await scenario.SaveApplicationAsync(targetId);
        var relationshipId = Uuid.CreateVersion4();
        var actor = scenario.Actor();
        var add = new AddApplicationRelationship(scenario.TenantId, scenario.SourceId,
            relationshipId, 0, targetId, "depends_on");
        var addHandler = scenario.AddHandler();

        // Act
        var added = await addHandler.HandleAsync(Scenario.Context(add, actor),
            CancellationToken.None);
        var removeHandler = scenario.RemoveHandler();
        var removed = await removeHandler.HandleAsync(Scenario.Context(
            new RemoveApplicationRelationship(scenario.TenantId, scenario.SourceId,
                relationshipId, 1), actor), CancellationToken.None);
        var persisted = await scenario.Reader.HydrateAsync(new DeclaredApplication(
            scenario.TenantId, scenario.SourceId));
        var events = await scenario.ReadSourceEventsAsync();

        // Assert
        Assert.True(added.IsSuccess);
        Assert.Equal("active", added.Value.State);
        Assert.Equal(1, added.Value.RelationshipRevision);
        Assert.True(removed.IsSuccess);
        Assert.Equal("removed", removed.Value.State);
        Assert.Equal(2, removed.Value.RelationshipRevision);
        Assert.Equal(1, persisted.Revision);
        Assert.Equal(2, persisted.SourceRelationshipRevision);
        Assert.Empty(persisted.ReadApplicationRelationships());
        var addedEvent = Assert.IsType<ApplicationRelationshipAdded>(
            Assert.Single(events, record => record.Event is ApplicationRelationshipAdded).Event);
        var removedEvent = Assert.IsType<ApplicationRelationshipRemoved>(
            Assert.Single(events, record => record.Event is ApplicationRelationshipRemoved).Event);
        var memberId = RbacIds.Member(scenario.TenantId, scenario.UserId);
        Assert.Equal(memberId, addedEvent.ActorMemberId);
        Assert.Equal(memberId, removedEvent.ActorMemberId);
        Assert.Equal(ActorReference.ForMember(memberId, "inventory@example.com"), addedEvent.Actor);
        Assert.Equal(ActorReference.ForMember(memberId, "inventory@example.com"), removedEvent.Actor);
    }

    [Fact]
    public async Task ShouldHideRestrictedAndCrossTenantTargetsWithoutAppendingGivenAddRequest()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var restrictedTargetId = Uuid.CreateVersion4();
        await scenario.SaveApplicationAsync(restrictedTargetId, isRestricted: true);
        var otherTenant = Uuid.CreateVersion4();
        var crossTenantTargetId = Uuid.CreateVersion4();
        await scenario.SaveApplicationAsync(crossTenantTargetId, otherTenant);
        var visibleTargetId = Uuid.CreateVersion4();
        await scenario.SaveApplicationAsync(visibleTargetId);
        var restrictedSourceId = Uuid.CreateVersion4();
        await scenario.SaveApplicationAsync(restrictedSourceId, isRestricted: true);
        var handler = scenario.AddHandler();
        var actor = scenario.Actor();
        var before = await scenario.ReadSourceEventsAsync();
        var restrictedSourceBefore = await scenario.ReadApplicationEventsAsync(
            scenario.TenantId, restrictedSourceId);

        // Act
        var restricted = await handler.HandleAsync(Scenario.Context(
            new AddApplicationRelationship(scenario.TenantId, scenario.SourceId,
                Uuid.CreateVersion4(), 0, restrictedTargetId, "replaces"), actor),
            CancellationToken.None);
        var crossTenant = await handler.HandleAsync(Scenario.Context(
            new AddApplicationRelationship(scenario.TenantId, scenario.SourceId,
                Uuid.CreateVersion4(), 0, crossTenantTargetId, "replaces"), actor),
            CancellationToken.None);
        var restrictedSource = await handler.HandleAsync(Scenario.Context(
            new AddApplicationRelationship(scenario.TenantId, restrictedSourceId,
                Uuid.CreateVersion4(), 0, visibleTargetId, "replaces"), actor),
            CancellationToken.None);
        var after = await scenario.ReadSourceEventsAsync();
        var restrictedSourceAfter = await scenario.ReadApplicationEventsAsync(
            scenario.TenantId, restrictedSourceId);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(restricted.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(crossTenant.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(restrictedSource.Error).Kind);
        Assert.Single(before);
        Assert.Single(after);
        Assert.Single(restrictedSourceBefore);
        Assert.Single(restrictedSourceAfter);
    }

    [Fact]
    public async Task ShouldRejectMutationOfRetiredSourceWithoutAppendingGivenAddRequest()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var targetId = Uuid.CreateVersion4();
        await scenario.SaveApplicationAsync(targetId);
        var source = await scenario.Reader.HydrateAsync(new DeclaredApplication(
            scenario.TenantId, scenario.SourceId));
        Assert.Null(source.Retire(1, DateTimeOffset.UtcNow, "No longer active", null,
            scenario.UserId, "Inventory manager", DateTimeOffset.UtcNow));
        await scenario.Writer.SaveAsync(source, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var before = await scenario.ReadSourceEventsAsync();

        // Act
        var result = await scenario.AddHandler().HandleAsync(Scenario.Context(
            new AddApplicationRelationship(scenario.TenantId, scenario.SourceId,
                Uuid.CreateVersion4(), 0, targetId, "depends_on"), scenario.Actor()),
            CancellationToken.None);
        var after = await scenario.ReadSourceEventsAsync();
        var persisted = await scenario.Reader.HydrateAsync(new DeclaredApplication(
            scenario.TenantId, scenario.SourceId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(2, before.Count);
        Assert.Equal(before.Count, after.Count);
        Assert.True(persisted.IsRetired);
        Assert.Equal(0, persisted.SourceRelationshipRevision);
    }

    [Fact]
    public async Task ShouldOmitHiddenTargetsAndRejectStaleReadContinuationGivenRelationshipPages()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var visibleTargetA = Uuid.CreateVersion4();
        var visibleTargetB = Uuid.CreateVersion4();
        var hiddenTarget = Uuid.CreateVersion4();
        await scenario.SaveApplicationAsync(visibleTargetA);
        await scenario.SaveApplicationAsync(visibleTargetB);
        await scenario.SaveApplicationAsync(hiddenTarget, isRestricted: true);
        var source = await scenario.Reader.HydrateAsync(new DeclaredApplication(
            scenario.TenantId, scenario.SourceId));
        Assert.True(source.AddApplicationRelationship(0, Uuid.CreateVersion4(),
            visibleTargetA, "depends_on", scenario.UserId, "Inventory manager",
            DateTimeOffset.UtcNow).IsSuccess);
        Assert.True(source.AddApplicationRelationship(1, Uuid.CreateVersion4(),
            visibleTargetB, "replaces", scenario.UserId, "Inventory manager",
            DateTimeOffset.UtcNow).IsSuccess);
        Assert.True(source.AddApplicationRelationship(2, Uuid.CreateVersion4(),
            hiddenTarget, "depends_on", scenario.UserId, "Inventory manager",
            DateTimeOffset.UtcNow).IsSuccess);
        await scenario.Writer.SaveAsync(source, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var handler = scenario.ListHandler();
        var actor = scenario.Actor();

        // Act
        var first = await handler.HandleAsync(Scenario.Context(
            new ListApplicationRelationships(scenario.TenantId, scenario.SourceId,
                Limit: 1, MinimumRevision: 2), actor), CancellationToken.None);
        var second = await handler.HandleAsync(Scenario.Context(
            new ListApplicationRelationships(scenario.TenantId, scenario.SourceId,
                Limit: 1, Cursor: first.Value.NextCursor, MinimumRevision: 2), actor),
            CancellationToken.None);
        var lagging = await handler.HandleAsync(Scenario.Context(
            new ListApplicationRelationships(scenario.TenantId, scenario.SourceId,
                MinimumRevision: 4), actor), CancellationToken.None);
        Assert.True(source.AddApplicationRelationship(3, Uuid.CreateVersion4(),
            visibleTargetA, "replaces", scenario.UserId, "Inventory manager",
            DateTimeOffset.UtcNow).IsSuccess);
        await scenario.Writer.SaveAsync(source, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var staleCursor = await handler.HandleAsync(Scenario.Context(
            new ListApplicationRelationships(scenario.TenantId, scenario.SourceId,
                Limit: 1, Cursor: first.Value.NextCursor), actor), CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, first.Value.RelationshipRevision);
        Assert.Equal(2, second.Value.RelationshipRevision);
        Assert.NotNull(first.Value.NextCursor);
        Assert.Null(second.Value.NextCursor);
        var visibleIds = first.Value.Items.Concat(second.Value.Items)
            .Select(item => item.TargetApplicationId).ToArray();
        Assert.Equal(2, visibleIds.Length);
        Assert.Contains(visibleTargetA, visibleIds);
        Assert.Contains(visibleTargetB, visibleIds);
        Assert.DoesNotContain(hiddenTarget, visibleIds);
        var laggingError = Assert.IsType<RequestError>(lagging.Error);
        Assert.Equal(RequestErrorKind.Conflict, laggingError.Kind);
        Assert.True(laggingError.IsTransient);
        Assert.Contains("visible relationship revision 2", laggingError.Message,
            StringComparison.Ordinal);
        Assert.Contains("minimum revision 4", laggingError.Message, StringComparison.Ordinal);
        var staleError = Assert.IsType<RequestError>(staleCursor.Error);
        Assert.Equal(RequestErrorKind.Conflict, staleError.Kind);
        Assert.True(staleError.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnCurrentSnapshotGivenMinimumRevisionZero()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var handler = scenario.ListHandler();

        // Act
        var result = await handler.HandleAsync(Scenario.Context(
            new ListApplicationRelationships(scenario.TenantId, scenario.SourceId,
                MinimumRevision: 0), scenario.Actor()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.RelationshipRevision);
        Assert.Empty(result.Value.Items);
        Assert.Null(result.Value.NextCursor);
    }

    [Fact]
    public async Task ShouldCountOnlyCurrentlyVisibleRelationshipEventsGivenList()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var visibleTargetA = Uuid.CreateVersion4();
        var visibleTargetB = Uuid.CreateVersion4();
        var hiddenTarget = Uuid.CreateVersion4();
        await scenario.SaveApplicationAsync(visibleTargetA);
        await scenario.SaveApplicationAsync(visibleTargetB);
        await scenario.SaveApplicationAsync(hiddenTarget, isRestricted: true);
        var source = await scenario.Reader.HydrateAsync(new DeclaredApplication(
            scenario.TenantId, scenario.SourceId));
        var now = DateTimeOffset.UtcNow;
        var hiddenRelationshipId = Uuid.CreateVersion4();
        Assert.True(source.AddApplicationRelationship(0, Uuid.CreateVersion4(), visibleTargetA,
            "depends_on", scenario.UserId, "Inventory manager", now).IsSuccess);
        Assert.True(source.AddApplicationRelationship(1, hiddenRelationshipId, hiddenTarget,
            "depends_on", scenario.UserId, "Inventory manager", now).IsSuccess);
        Assert.True(source.RemoveApplicationRelationship(2, hiddenRelationshipId,
            scenario.UserId, "Inventory manager", now).IsSuccess);
        Assert.True(source.AddApplicationRelationship(3, Uuid.CreateVersion4(), visibleTargetB,
            "replaces", scenario.UserId, "Inventory manager", now).IsSuccess);
        await scenario.Writer.SaveAsync(source, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);

        // Act
        var result = await scenario.ListHandler().HandleAsync(Scenario.Context(
            new ListApplicationRelationships(scenario.TenantId, scenario.SourceId),
            scenario.Actor()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.RelationshipRevision);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Contains(result.Value.Items, item => item.TargetApplicationId == visibleTargetA);
        Assert.Contains(result.Value.Items, item => item.TargetApplicationId == visibleTargetB);
        Assert.DoesNotContain(result.Value.Items, item => item.TargetApplicationId == hiddenTarget);
    }

    [Fact]
    public async Task ShouldUseVisibleEventCountForMutationAcknowledgementsGivenHiddenHistory()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var hiddenTarget = Uuid.CreateVersion4();
        var visibleTargetA = Uuid.CreateVersion4();
        var visibleTargetB = Uuid.CreateVersion4();
        await scenario.SaveApplicationAsync(hiddenTarget, isRestricted: true);
        await scenario.SaveApplicationAsync(visibleTargetA);
        await scenario.SaveApplicationAsync(visibleTargetB);
        var source = await scenario.Reader.HydrateAsync(new DeclaredApplication(
            scenario.TenantId, scenario.SourceId));
        var now = DateTimeOffset.UtcNow;
        var hiddenRelationshipId = Uuid.CreateVersion4();
        Assert.True(source.AddApplicationRelationship(0, hiddenRelationshipId, hiddenTarget,
            "depends_on", scenario.UserId, "Inventory manager", now).IsSuccess);
        Assert.True(source.RemoveApplicationRelationship(1, hiddenRelationshipId,
            scenario.UserId, "Inventory manager", now).IsSuccess);
        await scenario.Writer.SaveAsync(source, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var relationshipId = Uuid.CreateVersion4();
        var addHandler = scenario.AddHandler();

        // Act
        var added = await addHandler.HandleAsync(Scenario.Context(
            new AddApplicationRelationship(scenario.TenantId, scenario.SourceId,
                relationshipId, 0, visibleTargetA, "depends_on"), scenario.Actor()),
            CancellationToken.None);
        var addedRetry = await addHandler.HandleAsync(Scenario.Context(
            new AddApplicationRelationship(scenario.TenantId, scenario.SourceId,
                relationshipId, 0, visibleTargetA, "depends_on"), scenario.Actor()),
            CancellationToken.None);
        var removed = await scenario.RemoveHandler().HandleAsync(Scenario.Context(
            new RemoveApplicationRelationship(scenario.TenantId, scenario.SourceId,
                relationshipId, 1), scenario.Actor()), CancellationToken.None);
        var removedRetry = await scenario.RemoveHandler().HandleAsync(Scenario.Context(
            new RemoveApplicationRelationship(scenario.TenantId, scenario.SourceId,
                relationshipId, 1), scenario.Actor()), CancellationToken.None);

        // Assert
        Assert.True(added.IsSuccess);
        Assert.Equal(1, added.Value.RelationshipRevision);
        Assert.True(addedRetry.IsSuccess);
        Assert.Equal(1, addedRetry.Value.RelationshipRevision);
        Assert.True(removed.IsSuccess);
        Assert.Equal(2, removed.Value.RelationshipRevision);
        Assert.True(removedRetry.IsSuccess);
        Assert.Equal(2, removedRetry.Value.RelationshipRevision);
    }

    [Fact]
    public async Task ShouldReportOnlyVisibleRevisionGivenStaleMutationAndMinimum()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var hiddenTarget = Uuid.CreateVersion4();
        var visibleTarget = Uuid.CreateVersion4();
        var newTarget = Uuid.CreateVersion4();
        await scenario.SaveApplicationAsync(hiddenTarget, isRestricted: true);
        await scenario.SaveApplicationAsync(visibleTarget);
        await scenario.SaveApplicationAsync(newTarget);
        var source = await scenario.Reader.HydrateAsync(new DeclaredApplication(
            scenario.TenantId, scenario.SourceId));
        var now = DateTimeOffset.UtcNow;
        var hiddenRelationshipId = Uuid.CreateVersion4();
        Assert.True(source.AddApplicationRelationship(0, hiddenRelationshipId, hiddenTarget,
            "depends_on", scenario.UserId, "Inventory manager", now).IsSuccess);
        Assert.True(source.RemoveApplicationRelationship(1, hiddenRelationshipId,
            scenario.UserId, "Inventory manager", now).IsSuccess);
        Assert.True(source.AddApplicationRelationship(2, Uuid.CreateVersion4(), visibleTarget,
            "replaces", scenario.UserId, "Inventory manager", now).IsSuccess);
        await scenario.Writer.SaveAsync(source, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);

        // Act
        var futureExpectedMutation = await scenario.AddHandler().HandleAsync(Scenario.Context(
            new AddApplicationRelationship(scenario.TenantId, scenario.SourceId,
                Uuid.CreateVersion4(), 2, newTarget, "depends_on"), scenario.Actor()),
            CancellationToken.None);
        var staleMutation = await scenario.AddHandler().HandleAsync(Scenario.Context(
            new AddApplicationRelationship(scenario.TenantId, scenario.SourceId,
                Uuid.CreateVersion4(), 0, newTarget, "depends_on"), scenario.Actor()),
            CancellationToken.None);
        var belowMinimum = await scenario.ListHandler().HandleAsync(Scenario.Context(
            new ListApplicationRelationships(scenario.TenantId, scenario.SourceId,
                MinimumRevision: 2), scenario.Actor()), CancellationToken.None);

        // Assert
        var futureExpectedError = Assert.IsType<RequestError>(futureExpectedMutation.Error);
        Assert.Equal(RequestErrorKind.Conflict, futureExpectedError.Kind);
        Assert.True(futureExpectedError.IsTransient);
        Assert.Contains("Expected relationship revision 2 did not match current " +
            "actor-visible revision 1", futureExpectedError.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain("revision 3", futureExpectedError.Message,
            StringComparison.Ordinal);
        var staleError = Assert.IsType<RequestError>(staleMutation.Error);
        Assert.Equal(RequestErrorKind.Conflict, staleError.Kind);
        Assert.True(staleError.IsTransient);
        Assert.Contains("Expected relationship revision 0 did not match current " +
            "actor-visible revision 1", staleError.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("revision 3", staleError.Message,
            StringComparison.Ordinal);
        var minimumError = Assert.IsType<RequestError>(belowMinimum.Error);
        Assert.Equal(RequestErrorKind.Conflict, minimumError.Kind);
        Assert.True(minimumError.IsTransient);
        Assert.Contains("visible relationship revision 1", minimumError.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain("revision 3", minimumError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldBindContinuationToActorLimitAndVisibleRowsGivenPaging()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var visibleTargetA = Uuid.CreateVersion4();
        var visibleTargetB = Uuid.CreateVersion4();
        var hiddenTarget = Uuid.CreateVersion4();
        var newHiddenTarget = Uuid.CreateVersion4();
        var visibleTargetC = Uuid.CreateVersion4();
        await scenario.SaveApplicationAsync(visibleTargetA);
        await scenario.SaveApplicationAsync(visibleTargetB);
        await scenario.SaveApplicationAsync(hiddenTarget, isRestricted: true);
        await scenario.SaveApplicationAsync(newHiddenTarget, isRestricted: true);
        await scenario.SaveApplicationAsync(visibleTargetC);
        var source = await scenario.Reader.HydrateAsync(new DeclaredApplication(
            scenario.TenantId, scenario.SourceId));
        var now = DateTimeOffset.UtcNow;
        Assert.True(source.AddApplicationRelationship(0, Uuid.CreateVersion4(), visibleTargetA,
            "depends_on", scenario.UserId, "Inventory manager", now).IsSuccess);
        Assert.True(source.AddApplicationRelationship(1, Uuid.CreateVersion4(), visibleTargetB,
            "depends_on", scenario.UserId, "Inventory manager", now).IsSuccess);
        Assert.True(source.AddApplicationRelationship(2, Uuid.CreateVersion4(), hiddenTarget,
            "replaces", scenario.UserId, "Inventory manager", now).IsSuccess);
        await scenario.Writer.SaveAsync(source, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var handler = scenario.ListHandler();
        var actor = scenario.Actor();
        var first = await handler.HandleAsync(Scenario.Context(
            new ListApplicationRelationships(scenario.TenantId, scenario.SourceId, Limit: 1),
            actor), CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.NotNull(first.Value.NextCursor);

        // Act
        var wrongLimit = await handler.HandleAsync(Scenario.Context(
            new ListApplicationRelationships(scenario.TenantId, scenario.SourceId,
                Limit: 2, Cursor: first.Value.NextCursor), actor), CancellationToken.None);
        var otherActor = await handler.HandleAsync(Scenario.Context(
            new ListApplicationRelationships(scenario.TenantId, scenario.SourceId,
                Limit: 1, Cursor: first.Value.NextCursor),
            Scenario.Actor(Uuid.CreateVersion4())), CancellationToken.None);
        var latest = await scenario.Reader.HydrateAsync(new DeclaredApplication(
            scenario.TenantId, scenario.SourceId));
        Assert.True(latest.AddApplicationRelationship(latest.SourceRelationshipRevision,
            Uuid.CreateVersion4(), newHiddenTarget, "depends_on", scenario.UserId,
            "Inventory manager", now.AddMinutes(1)).IsSuccess);
        await scenario.Writer.SaveAsync(latest, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var afterHiddenUpdate = await handler.HandleAsync(Scenario.Context(
            new ListApplicationRelationships(scenario.TenantId, scenario.SourceId,
                Limit: 1, Cursor: first.Value.NextCursor), actor), CancellationToken.None);
        var tombstone = await scenario.Reader.HydrateAsync(new DeclaredApplication(
            scenario.TenantId, scenario.SourceId));
        var tombstoneId = Uuid.CreateVersion4();
        Assert.True(tombstone.AddApplicationRelationship(tombstone.SourceRelationshipRevision,
            tombstoneId, visibleTargetC, "replaces", scenario.UserId,
            "Inventory manager", now.AddMinutes(2)).IsSuccess);
        await scenario.Writer.SaveAsync(tombstone, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        tombstone = await scenario.Reader.HydrateAsync(new DeclaredApplication(
            scenario.TenantId, scenario.SourceId));
        Assert.True(tombstone.RemoveApplicationRelationship(tombstone.SourceRelationshipRevision,
            tombstoneId, scenario.UserId, "Inventory manager", now.AddMinutes(3)).IsSuccess);
        await scenario.Writer.SaveAsync(tombstone, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var afterVisibleTombstone = await handler.HandleAsync(Scenario.Context(
            new ListApplicationRelationships(scenario.TenantId, scenario.SourceId,
                Limit: 1, Cursor: first.Value.NextCursor), actor), CancellationToken.None);
        var changed = await scenario.Reader.HydrateAsync(new DeclaredApplication(
            scenario.TenantId, scenario.SourceId));
        Assert.True(changed.AddApplicationRelationship(changed.SourceRelationshipRevision,
            Uuid.CreateVersion4(), visibleTargetC, "replaces", scenario.UserId,
            "Inventory manager", now.AddMinutes(4)).IsSuccess);
        await scenario.Writer.SaveAsync(changed, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var afterVisibleUpdate = await handler.HandleAsync(Scenario.Context(
            new ListApplicationRelationships(scenario.TenantId, scenario.SourceId,
                Limit: 1, Cursor: first.Value.NextCursor), actor), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(wrongLimit.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(otherActor.Error).Kind);
        Assert.True(afterHiddenUpdate.IsSuccess);
        Assert.Equal(2, afterHiddenUpdate.Value.RelationshipRevision);
        Assert.Single(afterHiddenUpdate.Value.Items);
        var firstTarget = first.Value.Items[0].TargetApplicationId;
        var expectedContinuationTarget = firstTarget == visibleTargetA
            ? visibleTargetB
            : visibleTargetA;
        Assert.Equal(expectedContinuationTarget,
            afterHiddenUpdate.Value.Items[0].TargetApplicationId);
        var tombstoneError = Assert.IsType<RequestError>(afterVisibleTombstone.Error);
        Assert.Equal(RequestErrorKind.Conflict, tombstoneError.Kind);
        Assert.True(tombstoneError.IsTransient);
        var changedError = Assert.IsType<RequestError>(afterVisibleUpdate.Error);
        Assert.Equal(RequestErrorKind.Conflict, changedError.Kind);
        Assert.True(changedError.IsTransient);
    }

    [Fact]
    public async Task ShouldDefaultToFiftyAndRejectLimitsAboveTwoHundredGivenRelationshipList()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var source = await scenario.Reader.HydrateAsync(new DeclaredApplication(
            scenario.TenantId, scenario.SourceId));
        for (var index = 0; index < 51; index++)
        {
            var targetId = Uuid.CreateVersion5(scenario.SourceId, $"target:{index}");
            await scenario.SaveApplicationAsync(targetId);
            Assert.True(source.AddApplicationRelationship(index,
                Uuid.CreateVersion5(scenario.SourceId, $"relationship:{index}"),
                targetId, "depends_on", scenario.UserId, "Inventory manager",
                DateTimeOffset.UtcNow).IsSuccess);
        }
        await scenario.Writer.SaveAsync(source, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var handler = scenario.ListHandler();
        var actor = scenario.Actor();

        // Act
        var first = await handler.HandleAsync(Scenario.Context(
            new ListApplicationRelationships(scenario.TenantId, scenario.SourceId), actor),
            CancellationToken.None);
        var second = await handler.HandleAsync(Scenario.Context(
            new ListApplicationRelationships(scenario.TenantId, scenario.SourceId,
                Cursor: first.Value.NextCursor), actor), CancellationToken.None);
        var aboveCap = await handler.HandleAsync(Scenario.Context(
            new ListApplicationRelationships(scenario.TenantId, scenario.SourceId, Limit: 201),
            actor), CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(50, first.Value.Items.Count);
        Assert.NotNull(first.Value.NextCursor);
        Assert.Single(second.Value.Items);
        Assert.Null(second.Value.NextCursor);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(aboveCap.Error).Kind);
    }

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly InMemoryEventStore _events;

        Scenario(ServiceProvider provider, AsyncServiceScope scope, InMemoryEventStore events,
            Uuid tenantId, Uuid sourceId, Uuid userId)
        {
            _provider = provider;
            _scope = scope;
            _events = events;
            TenantId = tenantId;
            SourceId = sourceId;
            UserId = userId;
        }

        public Uuid TenantId { get; }
        public Uuid SourceId { get; }
        public Uuid UserId { get; }
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public IAggregateWriter Writer => _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        IAggregateExecutor Executor => _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        RestrictedApplicationVisibility Visibility =>
            RestrictedApplicationVisibilityFixture.Create(Reader);

        public static async Task<Scenario> CreateAsync()
        {
            var services = new ServiceCollection();
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton(TimeProvider.System);
            services.AddPortia();
            var provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true });
            var scope = provider.CreateAsyncScope();
            var scenario = new Scenario(provider, scope, events, Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), Uuid.CreateVersion4());
            await scenario.SaveApplicationAsync(scenario.SourceId);
            return scenario;
        }

        public async Task SaveApplicationAsync(Uuid applicationId, Uuid? tenantId = null,
            bool isRestricted = false)
        {
            var application = new DeclaredApplication(tenantId ?? TenantId, applicationId);
            Assert.True(application.Declare("Application", "Application description", null,
                UserId, "Inventory manager", DateTimeOffset.UtcNow,
                isRestricted: isRestricted).IsSuccess);
            await Writer.SaveAsync(application, new RequestDispatchContext(RequestActor.System),
                CancellationToken.None);
        }

        public AddApplicationRelationshipHandler AddHandler() =>
            new(Executor, Reader, Visibility, TimeProvider.System);

        public RemoveApplicationRelationshipHandler RemoveHandler() =>
            new(Executor, Reader, Visibility, TimeProvider.System);

        public ListApplicationRelationshipsHandler ListHandler() =>
            new(Reader, Visibility);

        public ClaimsPrincipal Actor() => Actor(UserId);

        public static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [
            new Claim("iss", "bdgrz"),
            new Claim("sub", userId.ToString()),
            new Claim("email", "inventory@example.com"),
        ], "BdgrzSession"));

        public static RequestContext<TRequest> Context<TRequest>(TRequest request,
            ClaimsPrincipal actor) where TRequest : IRequestBase =>
            new(request, actor);

        public Task<List<DomainEventRecord>> ReadSourceEventsAsync() =>
            ReadApplicationEventsAsync(TenantId, SourceId);

        public async Task<List<DomainEventRecord>> ReadApplicationEventsAsync(Uuid tenantId,
            Uuid applicationId)
        {
            var records = new List<DomainEventRecord>();
            await foreach (var record in _events.ReadAsync(new EventStreamAddress(
                               tenantId.ToString(), "applications", applicationId.ToString()),
                               0, CancellationToken.None))
                records.Add(record);
            return records;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class ScopedReadOnlyPermissions : IAccessGrantScopePermissionAuthorizer
    {
        public int Checks { get; private set; }

        public ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, IReadOnlyCollection<AccessGrantScope> scopes, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(false);

        public ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default)
        {
            Checks++;
            return ValueTask.FromResult(true);
        }
    }
}
