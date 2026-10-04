using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class TechnologyInventoryGrantBackfillReactorTests
{
    [Fact]
    public async Task ShouldGrantTechnologyInventoryPermissionToBuiltInManagersGivenHistoricalTenantRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme"));

        // Act
        await scenario.RunAsync(new TechnologyInventoryGrantBackfillReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        Assert.Equal(4, scenario.SentRequests.Count);
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            TenantId: var actualTenant,
            RoleId: var roleId,
            Permission: "technology_inventory.manage",
        } && actualTenant == tenantId &&
            roleId == BuiltInRbac.TenantAdministrationRoleId(tenantId));
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            TenantId: var actualTenant,
            RoleId: var roleId,
            Permission: "technology_inventory.manage",
        } && actualTenant == tenantId &&
            roleId == BuiltInRbac.ComplianceManagementRoleId(tenantId));
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            TenantId: var actualTenant,
            RoleId: var roleId,
            Permission: "technology_inventory.restricted.read",
        } && actualTenant == tenantId &&
            roleId == BuiltInRbac.TenantAdministrationRoleId(tenantId));
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            TenantId: var actualTenant,
            RoleId: var roleId,
            Permission: "technology_inventory.restricted.read",
        } && actualTenant == tenantId &&
            roleId == BuiltInRbac.ComplianceManagementRoleId(tenantId));
    }
}
