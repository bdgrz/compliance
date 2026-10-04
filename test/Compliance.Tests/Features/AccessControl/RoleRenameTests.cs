using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class RoleRenameTests
{
    [Fact]
    public void ShouldRecordRenameGivenAnExistingRole()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var roleId = Uuid.CreateVersion4();
        var role = new Role(tenantId, roleId);
        _ = role.Define("Old role name");
        var scenario = new AggregateScenario<Role>(role);

        // Act
        var result = role.Rename("New role name");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(scenario.PendingEvents, ev => ev is RoleRenamed
        {
            RoleId: var renamedId, Name: "New role name",
        } && renamedId == roleId);
    }

    [Fact]
    public void ShouldCreateRoleGivenMigrationForAnUninitializedTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var roleId = BuiltInRbac.TenantAdministrationRoleId(tenantId);
        var role = new Role(tenantId, roleId);
        var scenario = new AggregateScenario<Role>(role);

        // Act
        var result = role.Rename(BuiltInRbac.TenantAdministrationRoleName);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(scenario.PendingEvents, ev => ev is RoleDefined
        {
            RoleId: var definedId, Name: BuiltInRbac.TenantAdministrationRoleName,
        } && definedId == roleId);
    }

    [Fact]
    public void ShouldPreserveMigratedNameGivenBootstrapReplay()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var roleId = BuiltInRbac.ComplianceManagementRoleId(tenantId);
        var role = new Role(tenantId, roleId);
        var history = new AggregateScenario<Role>(role)
            .Given(DomainEventSeed.Attach(new RoleDefined(tenantId, roleId, "Compliance Manager"), roleId, 1))
            .Given(DomainEventSeed.Attach(new RoleRenamed(tenantId, roleId,
                BuiltInRbac.ComplianceManagementRoleName), roleId, 2));

        // Act: the bootstrap's original DefineRole command is replayed after migration.
        var result = role.Define("Compliance Manager");

        // Assert: it is idempotent and does not reintroduce the legacy name.
        Assert.True(result.IsSuccess);
        Assert.Empty(history.PendingEvents);
    }
}
