using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class FirmStaffDirectoryTests
{
    static readonly ActorReference Operator = ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator");
    static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldKeepCanonicalUserAndPracticeGivenDuplicateDirectoryIdentity()
    {
        // Arrange
        var directory = new FirmStaffDirectory();
        var staffId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        Assert.True(directory.Register(Uuid.CreateVersion4(), staffId, userId, "advisory", "Synthetic source",
            0, Operator, Now).IsSuccess);

        // Act
        var duplicate = directory.Register(Uuid.CreateVersion4(), Uuid.CreateVersion4(), userId,
            "attest", "Synthetic source", 1, Operator, Now);
        var rewrite = directory.Register(Uuid.CreateVersion4(), staffId, Uuid.CreateVersion4(),
            "attest", "Synthetic source", 1, Operator, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, duplicate.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, rewrite.Error?.Kind);
        Assert.Equal("advisory", directory.Get(staffId)!.Practice);
        Assert.Equal(userId, directory.Get(staffId)!.UserId);
        Assert.Single(new AggregateScenario<FirmStaffDirectory>(directory).PendingEvents);
    }
    [Fact]
    public void ShouldRetainCanonicalPracticeAndOriginalResponseGivenDeactivationReplayAndRetry()
    {
        // Arrange
        var source = new FirmStaffDirectory();
        var requestId = Uuid.CreateVersion4();
        var staffId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        Assert.True(source.Register(requestId, staffId, userId, "advisory", "Synthetic source", 0, Operator, Now).IsSuccess);
        Assert.True(source.SetStatus(Uuid.CreateVersion4(), staffId, false, "Synthetic deactivation", 1, Operator, Now).IsSuccess);
        var events = new AggregateScenario<FirmStaffDirectory>(source).PendingEvents.ToArray();
        var hydrated = new AggregateScenario<FirmStaffDirectory>(new FirmStaffDirectory()).Given(events).Aggregate;

        // Act
        var retry = hydrated.Register(requestId, staffId, userId, "advisory", "Synthetic source", 0, Operator, Now.AddDays(1));
        var rewrite = hydrated.Register(requestId, staffId, userId, "attest", "Synthetic source", 0, Operator, Now);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.True(retry.Value!.IsActive);
        Assert.Equal(Now, retry.Value.RecordedAt);
        Assert.False(hydrated.Get(staffId)!.IsActive);
        Assert.Equal("advisory", hydrated.Get(staffId)!.Practice);
        Assert.Empty(hydrated.View(activeOnly: true).Staff);
        Assert.Equal(RequestErrorKind.Conflict, rewrite.Error?.Kind);
        Assert.Equal(2, hydrated.Sequence);
        Assert.Empty(new AggregateScenario<FirmStaffDirectory>(hydrated).PendingEvents);
    }

    [Theory]
    [InlineData("empty_user")]
    [InlineData("invalid_practice")]
    [InlineData("nonoperator_actor")]
    public void ShouldRefuseMalformedMetadataGivenCorruptedDirectoryReplay(string corruption)
    {
        // Arrange
        var member = new FirmStaffMemberView(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "advisory",
            "Synthetic source", true, 1, Operator, Now);
        member = corruption switch
        {
            "empty_user" => member with { UserId = Uuid.Empty },
            "invalid_practice" => member with { Practice = "unknown" },
            _ => member with { Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Synthetic member") }
        };
        var ev = new FirmStaffChangeRecorded(Uuid.CreateVersion4(), 0, "register", null, member);

        // Act
        var replay = () => new AggregateScenario<FirmStaffDirectory>(new FirmStaffDirectory()).Given(ev);

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

}
