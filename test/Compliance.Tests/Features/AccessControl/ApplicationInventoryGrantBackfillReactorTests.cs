using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ApplicationInventoryGrantBackfillReactorTests
{
    [Fact]
    public async Task ShouldBackfillInventoryAndRestrictedReadPermissionsGivenHistoricalTenantRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme"));

        // Act
        await scenario.RunAsync(new ApplicationInventoryGrantBackfillReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        Assert.Equal(4, scenario.SentRequests.Count);
        Assert.All(scenario.SentRequests, request =>
        {
            var grant = Assert.IsType<AssignRolePermission>(request);
            Assert.Equal(tenantId, grant.TenantId);
            Assert.Contains(grant.Permission, new[]
            {
                RbacPermissions.ApplicationInventoryManage,
                RbacPermissions.ApplicationRestrictedRead,
            });
        });
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            RoleId: var roleId,
            Permission: RbacPermissions.ApplicationInventoryManage,
        } && roleId == BuiltInRbac.TenantAdministrationRoleId(tenantId));
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            RoleId: var roleId,
            Permission: RbacPermissions.ApplicationInventoryManage,
        } && roleId == BuiltInRbac.ComplianceManagementRoleId(tenantId));
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            RoleId: var roleId,
            Permission: RbacPermissions.ApplicationRestrictedRead,
        } && roleId == BuiltInRbac.TenantAdministrationRoleId(tenantId));
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            RoleId: var roleId,
            Permission: RbacPermissions.ApplicationRestrictedRead,
        } && roleId == BuiltInRbac.ComplianceManagementRoleId(tenantId));
    }
}
