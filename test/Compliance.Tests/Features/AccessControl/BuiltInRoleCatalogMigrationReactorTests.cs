using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class BuiltInRoleCatalogMigrationReactorTests
{
    [Fact]
    public async Task ShouldRenameStableRolesAndAddViewerGivenExistingTenantRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme"));

        // Act
        await scenario.RunAsync(new BuiltInRoleCatalogMigrationReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        Assert.Contains(scenario.SentRequests, request => request is RenameRole
        {
            RoleId: var roleId, Name: BuiltInRbac.TenantAdministrationRoleName,
        } && roleId == BuiltInRbac.TenantAdministrationRoleId(tenantId));
        Assert.Contains(scenario.SentRequests, request => request is DefineRole
        {
            RoleId: var roleId, Name: BuiltInRbac.ViewerRoleName,
        } && roleId == BuiltInRbac.ViewerRoleId(tenantId));
        Assert.Contains(scenario.SentRequests, request => request is AssignTeamRole
        {
            TeamId: var teamId, RoleId: var roleId,
        } && teamId == BuiltInRbac.ViewersTeamId(tenantId) && roleId == BuiltInRbac.ViewerRoleId(tenantId));
    }
}
