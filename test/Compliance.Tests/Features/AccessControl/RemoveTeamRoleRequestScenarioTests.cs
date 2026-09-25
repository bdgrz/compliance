using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class RemoveTeamRoleRequestScenarioTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldRemoveAnAssignedRoleGivenTenantRbacManagePermission()
    {
        // Arrange
        await using var provider = RbacManagementServices.Build(allowed: true,
            portia => portia.AddRequestHandler<RemoveTeamRoleHandler>());
        await Seed(provider);

        await RequestScenario.For(provider)
            .GivenActor(RbacManagementServices.Actor())
            // Act
            .When(new RemoveTeamRole(TenantId, TeamId, RoleId))
            // Assert
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectSuccess();
    }

    [Fact]
    public async Task ShouldDenyGivenAnActorWithoutTheTenantRbacManagePermission()
    {
        // Arrange
        await using var provider = RbacManagementServices.Build(allowed: false,
            portia => portia.AddRequestHandler<RemoveTeamRoleHandler>());
        await Seed(provider);

        await RequestScenario.For(provider)
            .GivenActor(RbacManagementServices.Actor())
            // Act
            .When(new RemoveTeamRole(TenantId, TeamId, RoleId))
            // Assert
            .ExpectDenied(RequestErrorKind.Forbidden)
            .ExpectNotHandled();
    }

    static Task Seed(ServiceProvider provider) => RbacManagementServices.SeedAsync(provider,
        new TeamRole(TenantId, TeamId, RoleId), teamRole => teamRole.Assign());
}
