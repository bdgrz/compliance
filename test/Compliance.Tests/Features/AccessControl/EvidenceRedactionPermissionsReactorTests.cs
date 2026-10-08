using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class EvidenceRedactionPermissionsReactorTests
{
    [Fact]
    public async Task ShouldPlanOnlyFourFixedRolePermissionsGivenRegisteredTenant()
    {
        // Arrange: the real reactor runner uses a capturing bus; no production authority is issued here.
        var tenant = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(new TenantRegistered(tenant, Uuid.CreateVersion4(), "Client", "client"));
        var reactor = new EvidenceRedactionPermissionsReactor(new InMemoryProjectionCheckpointStore(), scenario.Requests);

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        Assert.Equal("EvidenceRedactionPermissionsV1", reactor.Name);
        Assert.Equal(EventStreamPattern.ForPattern("bdgrz", "tenants"), reactor.Pattern);
        var commands = scenario.SentRequests.Select(Assert.IsType<AssignRolePermission>).ToArray();
        Assert.Equal(4, commands.Length);
        Assert.All(commands, command => Assert.Equal(tenant, command.TenantId));
        Assert.Equal(new[]
        {
            (BuiltInRbac.TenantAdministrationRoleId(tenant), RbacPermissions.EvidenceRedactionPrepare),
            (BuiltInRbac.ComplianceManagementRoleId(tenant), RbacPermissions.EvidenceRedactionPrepare),
            (BuiltInRbac.ComplianceParticipationRoleId(tenant), RbacPermissions.EvidenceRedactionPrepare),
            (BuiltInRbac.ComplianceManagementRoleId(tenant), RbacPermissions.EvidenceRedactionApprove)
        }, commands.Select(command => (command.RoleId, command.Permission)));
    }

    [Fact]
    public async Task ShouldPreserveLegacyCheckpointAndRepeatOnlySamePermissionPlanGivenCompletedCatalogReplay()
    {
        // Arrange: ReactorScenario intentionally redelivers source events on each run; its bus captures plans.
        var tenant = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(new TenantRegistered(tenant, Uuid.CreateVersion4(), "Client", "client"));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var legacy = new BuiltInRoleCatalogMigrationReactor(checkpoints, scenario.Requests);
        await scenario.RunAsync(legacy);
        var legacyIdentity = new CheckpointIdentity(legacy.Name, legacy.Pattern);
        var completedLegacy = await checkpoints.LoadAsync(legacyIdentity);
        var previousPlans = scenario.SentRequests.Count;
        var reactor = new EvidenceRedactionPermissionsReactor(checkpoints, scenario.Requests);
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);

        // Act
        await scenario.RunAsync(reactor);
        var first = scenario.SentRequests.Skip(previousPlans).Select(Assert.IsType<AssignRolePermission>).ToArray();
        var checkpoint = await checkpoints.LoadAsync(identity);
        await scenario.RunAsync(reactor);
        var replay = scenario.SentRequests.Skip(previousPlans + first.Length).Select(Assert.IsType<AssignRolePermission>).ToArray();

        // Assert
        Assert.Equal("BuiltInRoleCatalogV1", legacy.Name);
        Assert.NotEqual(legacyIdentity, identity);
        Assert.NotEqual(ProjectionCheckpoint.Start, completedLegacy);
        Assert.Equal(completedLegacy, await checkpoints.LoadAsync(legacyIdentity));
        Assert.NotEqual(ProjectionCheckpoint.Start, checkpoint);
        Assert.Equal(checkpoint, await checkpoints.LoadAsync(identity));
        Assert.Equal(4, first.Length);
        Assert.Equal(first, replay);
    }
}
