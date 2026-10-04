using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class TenantRbacBootstrapReactorTests
{
    [Fact]
    public async Task ShouldLeaveInventoryGrantToDedicatedReactorGivenRegisteredTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, ownerId, "Acme", "acme"));

        // Act
        await scenario.RunAsync(new TenantRbacBootstrapReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        Assert.Contains(scenario.SentRequests, request => request is DefineTeam);
        Assert.DoesNotContain(scenario.SentRequests, request => request is AssignRolePermission
        {
            Permission: RbacPermissions.ApplicationInventoryManage,
        });
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

    [Fact]
    public async Task ShouldBootstrapCreatorGivenVerifiedSelfServiceRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, creatorId, "Acme", "acme",
            "Acme LLC", CreatorIsAdministrator: true, ActivationRequired: true));

        // Act
        await scenario.RunAsync(new TenantRbacBootstrapReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        Assert.Contains(scenario.SentRequests, request => request is RegisterMember
        {
            UserId: var userId, Affiliation: "client_personnel",
        } && userId == creatorId);
        Assert.Contains(scenario.SentRequests, request => request is AssignTeamMember
        {
            TeamId: var teamId, MemberId: var memberId,
        } && teamId == BuiltInRbac.AdministratorsTeamId(tenantId) &&
            memberId == RbacIds.Member(tenantId, creatorId));
        Assert.DoesNotContain(scenario.SentRequests, request => request is ActivateTenant);
    }

    [Fact]
    public async Task ShouldBootstrapLaterTenantGivenEarlierSelfServiceActivationIsTransient()
    {
        // Arrange
        var firstTenantId = Uuid.CreateVersion4();
        var firstCreatorId = Uuid.CreateVersion4();
        var laterTenantId = Uuid.CreateVersion4();
        var laterCreatorId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(firstTenantId, firstCreatorId, "First", "first",
                "First LLC", CreatorIsAdministrator: true, ActivationRequired: true),
            new TenantRegistered(laterTenantId, laterCreatorId, "Later", "later",
                "Later LLC", CreatorIsAdministrator: true, ActivationRequired: true))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The first administrator is still being provisioned.", isTransient: true)));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TenantRbacBootstrapReactor(checkpoints, scenario.Requests);

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        Assert.Contains(scenario.SentRequests, request => request is AssignRolePermission
        {
            TenantId: var tenantId,
            Permission: RbacPermissions.TenantRbacManage,
        } && tenantId == laterTenantId);
        Assert.DoesNotContain(scenario.SentRequests, request => request is ActivateTenant);
        Assert.NotEqual(ProjectionCheckpoint.Start,
            await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern)));
    }

    [Fact]
    public async Task ShouldPreserveInvitationBootstrapGivenLegacyRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var operatorId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, operatorId, "Acme", "acme",
            "Acme LLC", "admin@example.com"));

        // Act
        await scenario.RunAsync(new TenantRbacBootstrapReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        Assert.Contains(scenario.SentRequests, request => request is DefineTeam);
        Assert.DoesNotContain(scenario.SentRequests, request => request is RegisterMember);
        Assert.DoesNotContain(scenario.SentRequests, request => request is AssignTeamMember);
    }
}
