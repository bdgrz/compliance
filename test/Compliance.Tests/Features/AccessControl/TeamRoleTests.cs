using System.Globalization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class TeamRoleTests
{
    static readonly Uuid TenantId = Uuid.Parse("11f455d2-fb10-4f28-a157-23e18e706e70", CultureInfo.InvariantCulture);
    static readonly Uuid TeamId = Uuid.Parse("2cc8e854-a49a-42a1-9581-d0622de3d5c3", CultureInfo.InvariantCulture);
    static readonly Uuid RoleId = Uuid.Parse("c1b1b99d-2c52-46c6-ad58-55fe90bf53b3", CultureInfo.InvariantCulture);

    [Fact]
    public void ShouldAssignIdempotently()
    {
        var teamRole = new TeamRole(TenantId, TeamId, RoleId);
        var scenario = new AggregateScenario<TeamRole>(teamRole);

        var first = scenario.Aggregate.Assign();
        var second = scenario.Aggregate.Assign();

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        var assigned = Assert.Single(scenario.PendingEvents);
        Assert.Equal("TeamRoleAssigned", assigned.GetType().Name);
    }

    [Fact]
    public void ShouldRemoveAnAssignedRole()
    {
        var teamRole = new TeamRole(TenantId, TeamId, RoleId);
        var scenario = new AggregateScenario<TeamRole>(teamRole)
            .Given(DomainEventSeed.Attach(new TeamRoleAssigned(TenantId, TeamId, RoleId), teamRole.Id, 1));

        var result = scenario.Aggregate.Remove();

        Assert.True(result.IsSuccess);
        var removed = Assert.Single(scenario.PendingEvents);
        Assert.Equal("TeamRoleRemoved", removed.GetType().Name);
    }

    [Fact]
    public void ShouldReturnNotFoundGivenAnUnassignedRole()
    {
        var teamRole = new TeamRole(TenantId, TeamId, RoleId);

        var result = teamRole.Remove();

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    public void ShouldReturnNotFoundGivenARepeatedRemoval()
    {
        var teamRole = new TeamRole(TenantId, TeamId, RoleId);
        var scenario = new AggregateScenario<TeamRole>(teamRole)
            .Given(
                DomainEventSeed.Attach(new TeamRoleAssigned(TenantId, TeamId, RoleId), teamRole.Id, 1),
                DomainEventSeed.Attach(new TeamRoleRemoved(TenantId, TeamId, RoleId), teamRole.Id, 2));

        var result = scenario.Aggregate.Remove();

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    public void ShouldAllowReassigningAfterRemoval()
    {
        var teamRole = new TeamRole(TenantId, TeamId, RoleId);
        var scenario = new AggregateScenario<TeamRole>(teamRole)
            .Given(
                DomainEventSeed.Attach(new TeamRoleAssigned(TenantId, TeamId, RoleId), teamRole.Id, 1),
                DomainEventSeed.Attach(new TeamRoleRemoved(TenantId, TeamId, RoleId), teamRole.Id, 2));

        var result = scenario.Aggregate.Assign();

        Assert.True(result.IsSuccess);
        var reassigned = Assert.Single(scenario.PendingEvents);
        Assert.Equal("TeamRoleAssigned", reassigned.GetType().Name);
    }
}
