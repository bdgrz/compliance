using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class WorkforceGrantBackfillReactorTests
{
    [Fact]
    public async Task ShouldGrantOnlyWorkforcePermissionGivenTenantRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme"));

        // Act
        await scenario.RunAsync(new WorkforceGrantBackfillReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        Assert.Equal(2, scenario.SentRequests.Count);
        Assert.All(scenario.SentRequests, request =>
        {
            var grant = Assert.IsType<AssignRolePermission>(request);
            Assert.Equal(tenantId, grant.TenantId);
            Assert.Equal(RbacPermissions.WorkforceManage, grant.Permission);
        });
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            RoleId: var roleId,
        } && roleId == BuiltInRbac.TenantAdministrationRoleId(tenantId));
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            RoleId: var roleId,
        } && roleId == BuiltInRbac.ComplianceManagementRoleId(tenantId));
    }
}
