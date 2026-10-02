using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory;

public sealed class InventoryRegisterAggregateTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid OwnerId = Uuid.CreateVersion4();
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldReplayIdenticalRecordAndRejectChangedContentGivenRecordedLocation()
    {
        // Arrange
        var register = new LocationRegister(TenantId);
        var id = Uuid.CreateVersion4();
        Assert.True(register.Record(id, Location("physical_site", " HQ "), Author, Now).IsSuccess);

        // Act
        var replay = register.Record(id, Location("physical_site", "HQ"), Author, Now);
        var changed = register.Record(id, Location("physical_site", "Annex"), Author, Now);

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        var recorded = Assert.IsType<LocationRevisionRecorded>(Assert.Single(
            new AggregateScenario<LocationRegister>(register).PendingEvents));
        Assert.Equal("HQ", recorded.Content.Name);
        Assert.Equal(TechnologyInventoryRules.Active, recorded.Content.Lifecycle);
        Assert.Equal(1, recorded.Revision);
        Assert.Equal(Author, recorded.Actor);
    }

    [Theory]
    [InlineData("datacenter", "HQ", null)]
    [InlineData("physical_site", "", null)]
    [InlineData("physical_site", "   ", null)]
    public void ShouldRejectBeforeAppendGivenInvalidLocationShape(string kind, string name,
        string? geography)
    {
        // Arrange
        var register = new LocationRegister(TenantId);
        var content = Location(kind, name) with { GeographyReference = geography };

        // Act
        var result = register.Record(Uuid.CreateVersion4(), content, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<LocationRegister>(register).PendingEvents);
    }

    [Fact]
    public void ShouldRejectDuplicateActiveNameGivenSameKindIgnoringCase()
    {
        // Arrange
        var register = new LocationRegister(TenantId);
        Assert.True(register.Record(Uuid.CreateVersion4(), Location("hosting_region", "us-east-1"),
            Author, Now).IsSuccess);

        // Act
        var duplicate = register.Record(Uuid.CreateVersion4(),
            Location("hosting_region", " US-EAST-1 "), Author, Now);
        var otherKind = register.Record(Uuid.CreateVersion4(), Location("physical_site", "us-east-1"),
            Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, duplicate.Error!.Kind);
        Assert.True(otherKind.IsSuccess);
    }

    [Fact]
    public void ShouldReleaseNameGivenRetiredLocationAndRejectReactivationCollision()
    {
        // Arrange
        var register = new LocationRegister(TenantId);
        var first = Uuid.CreateVersion4();
        Assert.True(register.Record(first, Location("physical_site", "HQ"), Author, Now).IsSuccess);
        Assert.Null(register.Revise(first, 1, c => c with { Lifecycle = "retired" }, Author, Now));

        // Act
        var reused = Uuid.CreateVersion4();
        var reuse = register.Record(reused, Location("physical_site", "HQ"), Author, Now);
        var reactivate = register.Revise(first, 2, c => c with { Lifecycle = "active" }, Author, Now);

        // Assert
        Assert.True(reuse.IsSuccess);
        Assert.Equal(CommandFailureCode.StateConflict, reactivate!.Code);
        Assert.Equal("retired", register.Get(first)!.Lifecycle);
    }

    [Fact]
    public void ShouldRejectRenameCollisionStaleRevisionAndKindChangeGivenExistingLocations()
    {
        // Arrange
        var register = new LocationRegister(TenantId);
        var id = Uuid.CreateVersion4();
        Assert.True(register.Record(id, Location("physical_site", "HQ"), Author, Now).IsSuccess);
        Assert.True(register.Record(Uuid.CreateVersion4(), Location("physical_site", "Annex"),
            Author, Now).IsSuccess);

        // Act
        var collision = register.Revise(id, 1, c => c with { Name = "annex" }, Author, Now);
        var stale = register.Revise(id, 7, c => c with { Name = "Main" }, Author, Now);
        var kind = register.Revise(id, 1, c => c with { Kind = "hosting_region" }, Author, Now);
        var missing = register.Revise(Uuid.CreateVersion4(), 1, c => c, Author, Now);
        var renamed = register.Revise(id, 1, c => c with { Name = "Main" }, Author, Now.AddMinutes(1));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, collision!.Code);
        Assert.Equal(CommandFailureCode.VersionConflict, stale!.Code);
        Assert.Equal(CommandFailureCode.InvalidContent, kind!.Code);
        Assert.Equal(CommandFailureCode.MissingRecord, missing!.Code);
        Assert.Null(renamed);
        Assert.Equal(2, register.RevisionOf(id));
    }

    [Fact]
    public void ShouldRebuildFromRetainedEventsGivenReplayedLocationHistory()
    {
        // Arrange
        var register = new LocationRegister(TenantId);
        var id = Uuid.CreateVersion4();
        Assert.True(register.Record(id, Location("physical_site", "HQ"), Author, Now).IsSuccess);
        Assert.Null(register.Revise(id, 1, c => c with { Name = "Main" }, Author, Now.AddMinutes(1)));
        var retained = new AggregateScenario<LocationRegister>(register).PendingEvents.ToArray();

        // Act
        var hydrated = new AggregateScenario<LocationRegister>(new LocationRegister(TenantId))
            .Given(retained).Aggregate;

        // Assert
        Assert.Equal(2, hydrated.RevisionOf(id));
        Assert.Equal("Main", hydrated.Get(id)!.Name);
        Assert.Equal(0, hydrated.RevisionOf(Uuid.CreateVersion4()));
    }

    [Fact]
    public void ShouldNotSeeLocationGivenOtherTenantRegister()
    {
        // Arrange
        var register = new LocationRegister(TenantId);
        var id = Uuid.CreateVersion4();
        Assert.True(register.Record(id, Location("physical_site", "HQ"), Author, Now).IsSuccess);
        var retained = new AggregateScenario<LocationRegister>(register).PendingEvents.ToArray();

        // Act
        var foreign = new LocationRegister(Uuid.CreateVersion4());

        // Assert
        Assert.Equal(0, foreign.RevisionOf(id));
        Assert.Throws<InvalidOperationException>(() =>
            new AggregateScenario<LocationRegister>(foreign).Given(retained));
        Assert.True(foreign.Record(Uuid.CreateVersion4(), Location("physical_site", "HQ"), Author,
            Now).IsSuccess);
    }

    [Fact]
    public void ShouldNormalizeAndReplayGivenRecordedProcess()
    {
        // Arrange
        var register = new OperationalProcessRegister(TenantId);
        var id = Uuid.CreateVersion4();

        // Act
        var recorded = register.Record(id, Process(" Onboarding ") with { Inputs = "  " }, Author, Now);
        var replay = register.Record(id, Process("Onboarding"), Author, Now);
        var changed = register.Record(id, Process("Offboarding"), Author, Now);

        // Assert
        Assert.True(recorded.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        var ev = Assert.IsType<OperationalProcessRevisionRecorded>(Assert.Single(
            new AggregateScenario<OperationalProcessRegister>(register).PendingEvents));
        Assert.Equal("Onboarding", ev.Content.Name);
        Assert.Null(ev.Content.Inputs);
    }

    [Theory]
    [InlineData("", "Purpose")]
    [InlineData("Name", "")]
    public void ShouldRejectBeforeAppendGivenInvalidProcessShape(string name, string purpose)
    {
        // Arrange
        var register = new OperationalProcessRegister(TenantId);

        // Act
        var result = register.Record(Uuid.CreateVersion4(),
            Process(name) with { Purpose = purpose }, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<OperationalProcessRegister>(register).PendingEvents);
    }

    [Fact]
    public void ShouldEnforceActiveNameUniquenessAndRetirementGivenProcesses()
    {
        // Arrange
        var register = new OperationalProcessRegister(TenantId);
        var first = Uuid.CreateVersion4();
        Assert.True(register.Record(first, Process("Backups"), Author, Now).IsSuccess);

        // Act
        var duplicate = register.Record(Uuid.CreateVersion4(), Process(" BACKUPS "), Author, Now);
        var retire = register.Revise(first, 1, c => c with { Lifecycle = "retired" }, Author, Now);
        var reuse = register.Record(Uuid.CreateVersion4(), Process("Backups"), Author, Now);
        var invalid = register.Revise(first, 2, c => c with { Lifecycle = "paused" }, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, duplicate.Error!.Kind);
        Assert.Null(retire);
        Assert.True(reuse.IsSuccess);
        Assert.Equal(CommandFailureCode.InvalidContent, invalid!.Code);
    }

    static LocationContent Location(string kind, string name) =>
        new(kind, name, null, OwnerId, TechnologyInventoryRules.Active);

    static OperationalProcessContent Process(string name) =>
        new(name, "Handle customer data", OwnerId, null, null, TechnologyInventoryRules.Active);
}
