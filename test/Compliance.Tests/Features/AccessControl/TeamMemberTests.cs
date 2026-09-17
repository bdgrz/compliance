using System.Globalization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class TeamMemberTests
{
    static readonly Uuid TenantId = Uuid.Parse("11f455d2-fb10-4f28-a157-23e18e706e70", CultureInfo.InvariantCulture);
    static readonly Uuid TeamId = Uuid.Parse("2cc8e854-a49a-42a1-9581-d0622de3d5c3", CultureInfo.InvariantCulture);
    static readonly Uuid MemberId = Uuid.Parse("0862062f-97e9-45de-a312-0f884c48180d", CultureInfo.InvariantCulture);

    [Fact]
    public void ShouldAssignIdempotently()
    {
        var teamMember = new TeamMember(TenantId, TeamId, MemberId);
        var scenario = new AggregateScenario<TeamMember>(teamMember);

        var first = scenario.Aggregate.Assign();
        var second = scenario.Aggregate.Assign();

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        var assigned = Assert.Single(scenario.PendingEvents);
        Assert.Equal("TeamMemberAssigned", assigned.GetType().Name);
    }

    [Fact]
    public void ShouldRemoveAnAssignedMember()
    {
        var teamMember = new TeamMember(TenantId, TeamId, MemberId);
        var scenario = new AggregateScenario<TeamMember>(teamMember)
            .Given(DomainEventSeed.Attach(new TeamMemberAssigned(TenantId, TeamId, MemberId), teamMember.Id, 1));

        var result = scenario.Aggregate.Remove();

        Assert.True(result.IsSuccess);
        var removed = Assert.Single(scenario.PendingEvents);
        Assert.Equal("TeamMemberRemoved", removed.GetType().Name);
    }

    [Fact]
    public void ShouldReturnNotFoundGivenAnUnassignedMember()
    {
        var teamMember = new TeamMember(TenantId, TeamId, MemberId);

        var result = teamMember.Remove();

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    public void ShouldReturnNotFoundGivenARepeatedRemoval()
    {
        var teamMember = new TeamMember(TenantId, TeamId, MemberId);
        var scenario = new AggregateScenario<TeamMember>(teamMember)
            .Given(
                DomainEventSeed.Attach(new TeamMemberAssigned(TenantId, TeamId, MemberId), teamMember.Id, 1),
                DomainEventSeed.Attach(new TeamMemberRemoved(TenantId, TeamId, MemberId), teamMember.Id, 2));

        var result = scenario.Aggregate.Remove();

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    public void ShouldAllowRejoiningAfterLeaving()
    {
        var teamMember = new TeamMember(TenantId, TeamId, MemberId);
        var scenario = new AggregateScenario<TeamMember>(teamMember)
            .Given(
                DomainEventSeed.Attach(new TeamMemberAssigned(TenantId, TeamId, MemberId), teamMember.Id, 1),
                DomainEventSeed.Attach(new TeamMemberRemoved(TenantId, TeamId, MemberId), teamMember.Id, 2));

        var result = scenario.Aggregate.Assign();

        Assert.True(result.IsSuccess);
        var rejoined = Assert.Single(scenario.PendingEvents);
        Assert.Equal("TeamMemberAssigned", rejoined.GetType().Name);
    }
}
