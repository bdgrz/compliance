using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class WorkforceRestrictedFieldGrantBackfillReactorTests
{
    [Fact]
    public async Task ShouldGrantPersonalDetailsReadToTenantAndComplianceManagementGivenTenantRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme"));

        // Act
        await scenario.RunAsync(new WorkforceRestrictedFieldGrantBackfillReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        var grants = scenario.SentRequests.OfType<AssignRolePermission>().ToArray();
        Assert.Contains(grants, grant => grant.TenantId == tenantId &&
            grant.RoleId == BuiltInRbac.TenantAdministrationRoleId(tenantId) &&
            grant.Permission == FieldClasses.WorkforcePersonalDetails.ReadPermission);
        Assert.Contains(grants, grant => grant.TenantId == tenantId &&
            grant.RoleId == BuiltInRbac.ComplianceManagementRoleId(tenantId) &&
            grant.Permission == FieldClasses.WorkforcePersonalDetails.ReadPermission);
    }

    [Fact]
    public async Task ShouldGrantManagerChainReadOnlyToTenantAdministrationGivenTenantRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme"));

        // Act
        await scenario.RunAsync(new WorkforceRestrictedFieldGrantBackfillReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        var grant = Assert.Single(scenario.SentRequests.OfType<AssignRolePermission>(),
            item => item.Permission == FieldClasses.WorkforceManagerChain.ReadPermission);
        Assert.Equal(tenantId, grant.TenantId);
        Assert.Equal(BuiltInRbac.TenantAdministrationRoleId(tenantId), grant.RoleId);
        Assert.Equal("field.workforce.manager_chain.read", grant.Permission);
        Assert.Equal(grant.Permission, FieldClasses.WorkforceManagerChain.ReadPermission);
    }
}
