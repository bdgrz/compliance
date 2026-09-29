using System.Globalization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class MemberLifecycleTests
{
    static readonly Uuid TenantId = Uuid.Parse("11f455d2-fb10-4f28-a157-23e18e706e70", CultureInfo.InvariantCulture);
    static readonly Uuid UserId = Uuid.Parse("0862062f-97e9-45de-a312-0f884c48180d", CultureInfo.InvariantCulture);
    static readonly Uuid ActorMemberId = Uuid.Parse("2cc8e854-a49a-42a1-9581-d0622de3d5c3", CultureInfo.InvariantCulture);
    static readonly DateTimeOffset ActionAt = new(2026, 9, 29, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRecordAttributedSuspensionGivenRegisteredMember()
    {
        // Arrange
        var member = new Member(TenantId, UserId);
        var scenario = new AggregateScenario<Member>(member)
            .Given(DomainEventSeed.Attach(new MemberRegistered(TenantId, member.Id, UserId), member.Id, 1));

        // Act
        var result = scenario.Aggregate.Suspend(ActorMemberId, "Alex Admin", ActionAt,
            "Employment ended.");

        // Assert
        Assert.True(result.IsSuccess);
        var suspended = Assert.IsType<MemberSuspended>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(TenantId, suspended.TenantId);
        Assert.Equal(member.Id, suspended.MemberId);
        Assert.Equal(UserId, suspended.UserId);
        Assert.Equal(ActorMemberId, suspended.SuspendedByMemberId);
        Assert.Equal("Alex Admin", suspended.SuspendedByDisplay);
        Assert.Equal(ActionAt, suspended.SuspendedAt);
        Assert.Equal("Employment ended.", suspended.Reason);
    }

    [Fact]
    public void ShouldRecordAttributedReinstatementGivenSuspendedMember()
    {
        // Arrange
        var member = new Member(TenantId, UserId);
        var scenario = new AggregateScenario<Member>(member).Given(
            DomainEventSeed.Attach(new MemberRegistered(TenantId, member.Id, UserId), member.Id, 1),
            DomainEventSeed.Attach(new MemberSuspended(TenantId, member.Id, UserId, ActorMemberId,
                "Alex Admin", ActionAt, "Employment ended."), member.Id, 2));

        // Act
        var result = scenario.Aggregate.Reinstate(ActorMemberId, "Alex Admin", ActionAt.AddHours(1));

        // Assert
        Assert.True(result.IsSuccess);
        var reinstated = Assert.IsType<MemberReinstated>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(TenantId, reinstated.TenantId);
        Assert.Equal(member.Id, reinstated.MemberId);
        Assert.Equal(UserId, reinstated.UserId);
        Assert.Equal("client_personnel", reinstated.Affiliation);
        Assert.Equal(ActorMemberId, reinstated.ReinstatedByMemberId);
        Assert.Equal("Alex Admin", reinstated.ReinstatedByDisplay);
        Assert.Equal(ActionAt.AddHours(1), reinstated.ReinstatedAt);
    }
}
