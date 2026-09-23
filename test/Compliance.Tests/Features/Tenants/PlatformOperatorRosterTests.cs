using System.Globalization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class PlatformOperatorRosterTests
{
    static readonly Uuid First = Uuid.Parse("852a1468-9236-4808-880a-582e37256b96", CultureInfo.InvariantCulture);
    static readonly Uuid Second = Uuid.Parse("28b82a3b-be8f-4407-aabe-fc270b442ef3", CultureInfo.InvariantCulture);
    static readonly Uuid Third = Uuid.Parse("59ebcdd4-b6c9-4fdc-b36d-1d8abef92160", CultureInfo.InvariantCulture);
    static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldNotRestoreRevokedOperatorGivenRepeatedConfigurationSeed()
    {
        // Arrange
        var roster = new PlatformOperatorRoster();
        var scenario = new AggregateScenario<PlatformOperatorRoster>(roster);
        Assert.True(roster.Seed([First, Second], Now).IsSuccess);
        Assert.True(roster.Revoke(Second, First, "Handed over operations", Now).IsSuccess);

        // Act
        Assert.True(roster.Seed([First, Second], Now.AddDays(1)).IsSuccess);

        // Assert
        Assert.False(roster.IsOperator(First));
        Assert.True(roster.IsOperator(Second));
        Assert.Single(scenario.PendingEvents.OfType<PlatformOperatorRosterSeeded>());
    }

    [Fact]
    public void ShouldRejectLastOperatorRevocationGivenSingleActiveOperator()
    {
        // Arrange
        var roster = new PlatformOperatorRoster();
        Assert.True(roster.Seed([First], Now).IsSuccess);

        // Act
        var result = roster.Revoke(First, First, "Leaving", Now.AddMinutes(1));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(roster.IsOperator(First));
        Assert.Empty(new AggregateScenario<PlatformOperatorRoster>(roster).PendingEvents
            .OfType<PlatformOperatorRevoked>());
    }

    [Fact]
    public void ShouldRecordActorSubjectTimeAndReasonGivenGrantAndRevocation()
    {
        // Arrange
        var roster = new PlatformOperatorRoster();
        var scenario = new AggregateScenario<PlatformOperatorRoster>(roster);
        Assert.True(roster.Seed([First], Now).IsSuccess);

        // Act
        Assert.True(roster.Grant(First, Second, "Cover operations", Now.AddMinutes(1)).IsSuccess);
        Assert.True(roster.Revoke(First, Second, "Coverage ended", Now.AddMinutes(2)).IsSuccess);

        // Assert
        var granted = Assert.Single(scenario.PendingEvents.OfType<PlatformOperatorGranted>());
        Assert.Equal((First, Second, "Cover operations", Now.AddMinutes(1)),
            (granted.ActorUserId, granted.SubjectUserId, granted.Reason, granted.OccurredAt));
        var revoked = Assert.Single(scenario.PendingEvents.OfType<PlatformOperatorRevoked>());
        Assert.Equal((First, Second, "Coverage ended", Now.AddMinutes(2)),
            (revoked.ActorUserId, revoked.SubjectUserId, revoked.Reason, revoked.OccurredAt));
    }

    [Fact]
    public void ShouldRejectGrantGivenRevokedActor()
    {
        // Arrange
        var roster = new PlatformOperatorRoster();
        Assert.True(roster.Seed([First, Second], Now).IsSuccess);
        Assert.True(roster.Revoke(Second, First, "Handoff", Now).IsSuccess);

        // Act
        var result = roster.Grant(First, Third, "Unauthorized", Now.AddMinutes(1));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
        Assert.False(roster.IsOperator(Third));
    }
}
