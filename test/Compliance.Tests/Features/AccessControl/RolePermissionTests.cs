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
    public void ShouldAssignOnceGivenRepeatedAssignment()
    {
        // Arrange
        var rolePermission = new RolePermission(TenantId, RoleId, Permission);
        var scenario = new AggregateScenario<RolePermission>(rolePermission);

        var first = scenario.Aggregate.Assign();

        // Act
        var second = scenario.Aggregate.Assign();

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        var assigned = Assert.Single(scenario.PendingEvents);
        Assert.Equal("RolePermissionAssigned", assigned.GetType().Name);
    }

    [Fact]
    public void ShouldRemovePermissionGivenExistingAssignment()
    {
        // Arrange
        var rolePermission = new RolePermission(TenantId, RoleId, Permission);
        var scenario = new AggregateScenario<RolePermission>(rolePermission)
            .Given(DomainEventSeed.Attach(
                new RolePermissionAssigned(TenantId, RoleId, Permission), rolePermission.Id, 1));

        // Act
        var result = scenario.Aggregate.Remove();

        // Assert
        Assert.True(result.IsSuccess);
        var removed = Assert.Single(scenario.PendingEvents);
        Assert.Equal("RolePermissionRemoved", removed.GetType().Name);
    }

    [Fact]
    public void ShouldReturnNotFoundGivenAnUnassignedPermission()
    {
        // Arrange
        var rolePermission = new RolePermission(TenantId, RoleId, Permission);

        // Act
        var result = rolePermission.Remove();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    public void ShouldReturnNotFoundGivenARepeatedRemoval()
    {
        // Arrange
        var rolePermission = new RolePermission(TenantId, RoleId, Permission);
        var scenario = new AggregateScenario<RolePermission>(rolePermission)
            .Given(
                DomainEventSeed.Attach(new RolePermissionAssigned(TenantId, RoleId, Permission), rolePermission.Id, 1),
                DomainEventSeed.Attach(new RolePermissionRemoved(TenantId, RoleId, Permission), rolePermission.Id, 2));

        // Act
        var result = scenario.Aggregate.Remove();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    public void ShouldAllowReassignmentGivenPriorRemoval()
    {
        // Arrange
        var rolePermission = new RolePermission(TenantId, RoleId, Permission);
        var scenario = new AggregateScenario<RolePermission>(rolePermission)
            .Given(
                DomainEventSeed.Attach(new RolePermissionAssigned(TenantId, RoleId, Permission), rolePermission.Id, 1),
                DomainEventSeed.Attach(new RolePermissionRemoved(TenantId, RoleId, Permission), rolePermission.Id, 2));

        // Act
        var result = scenario.Aggregate.Assign();

        // Assert
        Assert.True(result.IsSuccess);
        var reassigned = Assert.Single(scenario.PendingEvents);
        Assert.Equal("RolePermissionAssigned", reassigned.GetType().Name);
    }
}
