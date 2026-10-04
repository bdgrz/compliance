using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class BuiltInRbacTests
{
    [Fact]
    public void ShouldPreserveRoleIdsGivenTheAcceptedClientRoleCatalog()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();

        // Act
        var admin = BuiltInRbac.RoleIdForRole(tenantId, "org_admin");
        var lead = BuiltInRbac.RoleIdForRole(tenantId, "compliance_lead");
        var contributor = BuiltInRbac.RoleIdForRole(tenantId, "contributor");
        var viewer = BuiltInRbac.RoleIdForRole(tenantId, "viewer");

        // Assert
        Assert.Equal(BuiltInRbac.TenantAdministrationRoleId(tenantId), admin);
        Assert.Equal(BuiltInRbac.ComplianceManagementRoleId(tenantId), lead);
        Assert.Equal(BuiltInRbac.ComplianceParticipationRoleId(tenantId), contributor);
        Assert.NotNull(viewer);
        Assert.NotEqual(BuiltInRbac.TenantAdministrationRoleId(tenantId), viewer);
        Assert.Equal("Org Admin", BuiltInRbac.TenantAdministrationRoleName);
        Assert.Equal("Compliance Lead", BuiltInRbac.ComplianceManagementRoleName);
        Assert.Equal("Contributor", BuiltInRbac.ComplianceParticipationRoleName);
        Assert.True(BuiltInRbac.IsBuiltInRole(tenantId, viewer!.Value));
    }

    [Fact]
    public void ShouldResolveHistoricalRoleNamesGivenAnExistingClientContract()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();

        // Act
        var admin = BuiltInRbac.RoleIdForRole(tenantId, BuiltInRbac.TenantAdministrationRole);
        var lead = BuiltInRbac.RoleIdForRole(tenantId, BuiltInRbac.ComplianceManagementRole);
        var contributor = BuiltInRbac.RoleIdForRole(tenantId,
            BuiltInRbac.ComplianceParticipationRole);

        // Assert
        Assert.Equal(BuiltInRbac.TenantAdministrationRoleId(tenantId), admin);
        Assert.Equal(BuiltInRbac.ComplianceManagementRoleId(tenantId), lead);
        Assert.Equal(BuiltInRbac.ComplianceParticipationRoleId(tenantId), contributor);
    }
}
