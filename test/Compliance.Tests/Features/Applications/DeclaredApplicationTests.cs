using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class DeclaredApplicationTests
{
    [Fact]
    public void ShouldReplayHistoricalClearsAndRetainAdditionsGivenOldSerializedRevision()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var owner = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var payload = JsonSerializer.SerializeToNode(new ApplicationRevised(tenant, id, 2,
            "Legacy edit", "Legacy purpose", null, actor, "Legacy editor", now),
            ComplianceCoreJsonContext.Default.ApplicationRevised)!.AsObject();
        payload.Remove("application_kind");
        payload.Remove("criticality");
        var oldEvent = payload.Deserialize(ComplianceCoreJsonContext.Default.ApplicationRevised)!;
        var application = new DeclaredApplication(tenant, id);
        new AggregateScenario<DeclaredApplication>(application).Given(
            DomainEventSeed.Attach(new ApplicationDeclared(tenant, id, "Original", "Purpose", null,
                actor, "Original actor", now.AddMinutes(-1), "internal", owner, owner,
                ApplicationKind: "service", Criticality: "critical"), id, 1),
            DomainEventSeed.Attach(oldEvent, id, 2));

        // Act
        var incomplete = application.Revise(2, "Revised", "Purpose", null, actor, "Editor", now,
            governedRevisionsEnabled: true);
        var completed = application.Revise(2, "Revised", "Purpose", null, actor, "Editor", now,
            "internal", owner, owner, governedRevisionsEnabled: true);

        // Assert
        Assert.Null(oldEvent.ApplicationKind);
        Assert.Null(oldEvent.Criticality);
        Assert.Null(oldEvent.SystemOwnerPersonId);
        Assert.Null(oldEvent.AccessOwnerPersonId);
        Assert.Null(oldEvent.Classification);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(incomplete).Code);
        Assert.Null(completed);
        var revised = Assert.IsType<ApplicationRevised>(Assert.Single(
            new AggregateScenario<DeclaredApplication>(application).PendingEvents));
        Assert.Equal(3, revised.Revision);
        Assert.Equal("service", revised.ApplicationKind);
        Assert.Equal("critical", revised.Criticality);
        Assert.Equal(owner, revised.AccessOwnerPersonId);
    }

    [Theory]
    [InlineData("classification_missing")]
    [InlineData("classification_blank")]
    [InlineData("system_owner_missing")]
    [InlineData("access_owner_missing")]
    [InlineData("system_owner_empty")]
    [InlineData("access_owner_empty")]
    [InlineData("kind_missing")]
    [InlineData("kind_blank")]
    [InlineData("criticality_missing")]
    [InlineData("criticality_blank")]
    public void ShouldRejectIncompleteContentWithoutEventsGivenEnabledFreshDeclaration(string gap)
    {
        // Arrange
        var application = new DeclaredApplication(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var owner = Uuid.CreateVersion4();
        var classification = gap == "classification_missing" ? null :
            gap == "classification_blank" ? " \t " : "internal";
        Uuid? systemOwner = gap == "system_owner_missing" ? null :
            gap == "system_owner_empty" ? Uuid.Empty : owner;
        Uuid? accessOwner = gap == "access_owner_missing" ? null :
            gap == "access_owner_empty" ? Uuid.Empty : owner;
        var kind = gap == "kind_missing" ? null : gap == "kind_blank" ? " \t " : "service";
        var criticality = gap == "criticality_missing" ? null :
            gap == "criticality_blank" ? " \t " : "business critical";

        // Act
        var result = application.Declare("Payroll", "Run payroll", null,
            Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow, classification,
            systemOwner, accessOwner, applicationKind: kind, criticality: criticality,
            governedDeclarationsEnabled: true);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.False(application.IsCreated);
        Assert.Empty(new AggregateScenario<DeclaredApplication>(application).PendingEvents);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ShouldRejectOversizedAdditionsWithoutEventsGivenFreshDeclaration(bool enabled,
        bool oversizedKind)
    {
        // Arrange
        var application = new DeclaredApplication(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var owner = Uuid.CreateVersion4();
        var oversized = new string('x', 201);

        // Act
        var result = application.Declare("Payroll", "Run payroll", null,
            Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow, "internal", owner, owner,
            applicationKind: oversizedKind ? oversized : "service",
            criticality: oversizedKind ? "critical" : oversized, governedDeclarationsEnabled: enabled);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(new AggregateScenario<DeclaredApplication>(application).PendingEvents);
    }

    [Fact]
    public void ShouldReplayIdenticalLegacyDeclarationWithoutRetrofitGivenEnabledPolicy()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var payload = JsonSerializer.SerializeToNode(new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, actorId, "Original", now),
            ComplianceCoreJsonContext.Default.ApplicationDeclared)!.AsObject();
        payload.Remove("application_kind");
        payload.Remove("criticality");
        var legacy = payload.Deserialize(ComplianceCoreJsonContext.Default.ApplicationDeclared)!;
        var application = new DeclaredApplication(tenantId, applicationId);
        new AggregateScenario<DeclaredApplication>(application).Given(
            DomainEventSeed.Attach(legacy, applicationId, 1));

        // Act
        var identical = application.Declare(" Payroll ", " Run payroll ", null, actorId,
            "Retry", now.AddMinutes(1), governedDeclarationsEnabled: true);
        var changedKind = application.Declare("Payroll", "Run payroll", null, actorId,
            "Retry", now, applicationKind: "service", governedDeclarationsEnabled: true);
        var changedCriticality = application.Declare("Payroll", "Run payroll", null, actorId,
            "Retry", now, criticality: "critical", governedDeclarationsEnabled: true);

        // Assert
        Assert.Null(legacy.ApplicationKind);
        Assert.Null(legacy.Criticality);
        Assert.True(identical.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changedKind.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changedCriticality.Error).Kind);
        Assert.Equal(1, application.Revision);
        Assert.Empty(new AggregateScenario<DeclaredApplication>(application).PendingEvents);
    }

    [Fact]
    public void ShouldCompareNormalizedAdditionsGivenCompleteDeclarationRetry()
    {
        // Arrange
        var application = new DeclaredApplication(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var actorId = Uuid.CreateVersion4();
        var owner = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        Assert.True(application.Declare("Payroll", "Run payroll", null, actorId, "Manager",
            now, "internal", owner, owner, applicationKind: " service ", criticality: " critical ",
            governedDeclarationsEnabled: true).IsSuccess);

        // Act
        var identical = application.Declare("Payroll", "Run payroll", null, actorId, "Manager",
            now, "internal", owner, owner, applicationKind: "service", criticality: "critical",
            governedDeclarationsEnabled: true);
        var changed = application.Declare("Payroll", "Run payroll", null, actorId, "Manager",
            now, "internal", owner, owner, applicationKind: "service", criticality: "low",
            governedDeclarationsEnabled: true);

        // Assert
        Assert.True(identical.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        var declared = Assert.IsType<ApplicationDeclared>(Assert.Single(
            new AggregateScenario<DeclaredApplication>(application).PendingEvents));
        Assert.Equal("service", declared.ApplicationKind);
        Assert.Equal("critical", declared.Criticality);
    }

    [Fact]
    public void ShouldPreserveHistoricalApplicationRevisionGivenLegacyInstanceEvent()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var application = new DeclaredApplication(tenantId, applicationId);

        new AggregateScenario<DeclaredApplication>(application).Given(
            DomainEventSeed.Attach(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", "Operations", actorId, "Manager", now),
                applicationId, 1),
            DomainEventSeed.Attach(new ApplicationRevised(tenantId, applicationId, 2,
                "Payroll", "Run monthly payroll", "Operations", actorId, "Manager",
                now.AddMinutes(1)), applicationId, 2),
            DomainEventSeed.Attach(new SystemInstanceDeclared(tenantId, applicationId,
                instanceId, 3, "Production", "production", null, "payroll-prod",
                actorId, "Manager", now.AddMinutes(2)), applicationId, 3));

        // Act
        var createReplay = application.Declare("Payroll", "Run payroll", "Operations",
            actorId, "Manager", now.AddMinutes(3));
        var stale = application.Revise(2, "Payroll", "Stale edit", null,
            actorId, "Manager", now.AddMinutes(3));
        var current = application.Revise(3, "Payroll", "Current edit", null,
            actorId, "Manager", now.AddMinutes(4));

        // Assert
        Assert.True(createReplay.IsSuccess);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(3, Assert.IsType<CommandFailure>(stale).Version!.CurrentRevision);
        Assert.Null(current);
        Assert.Equal(4, application.Revision);
        var revisedEvent = Assert.IsType<ApplicationRevised>(Assert.Single(
            new AggregateScenario<DeclaredApplication>(application).PendingEvents));
        Assert.Equal(4, revisedEvent.Revision);
        Assert.Equal(ActorReference.ForMember(actorId, "Manager"),
            revisedEvent.StoredActor);
        Assert.Equal(actorId.ToString(), revisedEvent.Actor.Id);
        Assert.Equal("Manager", revisedEvent.Actor.Display);
    }

    [Fact]
    public void ShouldValidateRequiredFieldsGivenApplicationDeclaration()
    {
        // Arrange
        var application = new DeclaredApplication(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;

        // Act
        var missingPurpose = application.Declare("Payroll", " ", null, actorId, "Manager", now);
        var created = application.Declare("Payroll", "Run payroll", null, actorId, "Manager", now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(missingPurpose.Error).Kind);
        Assert.True(created.IsSuccess);
        var createdEvent = Assert.IsType<ApplicationDeclared>(Assert.Single(
            new AggregateScenario<DeclaredApplication>(application).PendingEvents));
        Assert.Equal(ActorReference.ForMember(actorId, "Manager"), createdEvent.StoredActor);
    }

    [Fact]
    public void ShouldPreserveDeclaredClassificationGivenReplayAndRevision()
    {
        // Arrange
        var application = new DeclaredApplication(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;

        // Act
        var declared = application.Declare("Payroll", "Run payroll", "Finance", actorId,
            "Manager", now, " Internal ");
        var replay = application.Declare("Payroll", "Run payroll", "Finance", actorId,
            "Manager", now, " Internal ");
        var revised = application.Revise(1, "Payroll", "Run payroll", "Finance", actorId,
            "Manager", now.AddMinutes(1), " Restricted ");
        var oversized = application.Revise(2, "Payroll", "Run payroll", "Finance", actorId,
            "Manager", now.AddMinutes(2), new string('x', 201));

        // Assert
        Assert.True(declared.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Null(revised);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(oversized).Code);
        Assert.Collection(new AggregateScenario<DeclaredApplication>(application).PendingEvents,
            ev => Assert.Equal("Internal", Assert.IsType<ApplicationDeclared>(ev).Classification),
            ev => Assert.Equal("Restricted", Assert.IsType<ApplicationRevised>(ev).Classification));
    }

    [Fact]
    public void ShouldRetainExplicitRestrictionGivenApplicationDeclarationAndRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var application = new DeclaredApplication(tenantId, applicationId);

        // Act
        var declared = application.Declare("Payroll", "Run payroll", null, actorId,
            "Manager", now, isRestricted: true);
        var replay = application.Declare("Payroll", "Run payroll", null, actorId,
            "Manager", now, isRestricted: true);
        var revised = application.Revise(1, "Payroll", "Run payroll", null, actorId,
            "Manager", now.AddMinutes(1), isRestricted: false);

        // Assert
        Assert.True(declared.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Null(revised);
        Assert.False(application.IsRestricted);
        Assert.Collection(new AggregateScenario<DeclaredApplication>(application).PendingEvents,
            ev => Assert.True(Assert.IsType<ApplicationDeclared>(ev).IsRestricted),
            ev => Assert.False(Assert.IsType<ApplicationRevised>(ev).IsRestricted));
    }
}
