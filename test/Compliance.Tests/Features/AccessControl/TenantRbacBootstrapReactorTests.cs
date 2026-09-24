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
        Assert.Contains(scenario.SentRequests, request => request is ActivateTenant
        {
            FirstAdministratorUserId: var userId,
        } && userId == creatorId);
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
