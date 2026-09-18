using System.Globalization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class RolePermissionTests
{
    static readonly Uuid TenantId = Uuid.Parse("11f455d2-fb10-4f28-a157-23e18e706e70", CultureInfo.InvariantCulture);
    static readonly Uuid RoleId = Uuid.Parse("c1b1b99d-2c52-46c6-ad58-55fe90bf53b3", CultureInfo.InvariantCulture);
    const string Permission = "controls.read";

    [Fact]
    public void ShouldAssignIdempotently()
    {
        var rolePermission = new RolePermission(TenantId, RoleId, Permission);
        var scenario = new AggregateScenario<RolePermission>(rolePermission);

        var first = scenario.Aggregate.Assign();
        var second = scenario.Aggregate.Assign();

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        var assigned = Assert.Single(scenario.PendingEvents);
        Assert.Equal("RolePermissionAssigned", assigned.GetType().Name);
    }

    [Fact]
    public void ShouldRemoveAnAssignedPermission()
    {
        var rolePermission = new RolePermission(TenantId, RoleId, Permission);
        var scenario = new AggregateScenario<RolePermission>(rolePermission)
            .Given(DomainEventSeed.Attach(
                new RolePermissionAssigned(TenantId, RoleId, Permission), rolePermission.Id, 1));

        var result = scenario.Aggregate.Remove();

        Assert.True(result.IsSuccess);
        var removed = Assert.Single(scenario.PendingEvents);
        Assert.Equal("RolePermissionRemoved", removed.GetType().Name);
    }

    [Fact]
    public void ShouldReturnNotFoundGivenAnUnassignedPermission()
    {
        var rolePermission = new RolePermission(TenantId, RoleId, Permission);

        var result = rolePermission.Remove();

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    public void ShouldReturnNotFoundGivenARepeatedRemoval()
    {
        var rolePermission = new RolePermission(TenantId, RoleId, Permission);
        var scenario = new AggregateScenario<RolePermission>(rolePermission)
            .Given(
                DomainEventSeed.Attach(new RolePermissionAssigned(TenantId, RoleId, Permission), rolePermission.Id, 1),
                DomainEventSeed.Attach(new RolePermissionRemoved(TenantId, RoleId, Permission), rolePermission.Id, 2));

        var result = scenario.Aggregate.Remove();

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    public void ShouldAllowReassigningAfterRemoval()
    {
        var rolePermission = new RolePermission(TenantId, RoleId, Permission);
        var scenario = new AggregateScenario<RolePermission>(rolePermission)
            .Given(
                DomainEventSeed.Attach(new RolePermissionAssigned(TenantId, RoleId, Permission), rolePermission.Id, 1),
                DomainEventSeed.Attach(new RolePermissionRemoved(TenantId, RoleId, Permission), rolePermission.Id, 2));

        var result = scenario.Aggregate.Assign();

        Assert.True(result.IsSuccess);
        var reassigned = Assert.Single(scenario.PendingEvents);
        Assert.Equal("RolePermissionAssigned", reassigned.GetType().Name);
    }
}
