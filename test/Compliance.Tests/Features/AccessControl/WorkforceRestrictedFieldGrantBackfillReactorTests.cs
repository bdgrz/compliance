using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class WorkforceRestrictedFieldGrantBackfillReactorTests
{
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
        var grant = Assert.IsType<AssignRolePermission>(Assert.Single(scenario.SentRequests));
        Assert.Equal(tenantId, grant.TenantId);
        Assert.Equal(BuiltInRbac.TenantAdministrationRoleId(tenantId), grant.RoleId);
        Assert.Equal("field.workforce.manager_chain.read", grant.Permission);
        Assert.Equal(grant.Permission, FieldClasses.WorkforceManagerChain.ReadPermission);
    }
}
