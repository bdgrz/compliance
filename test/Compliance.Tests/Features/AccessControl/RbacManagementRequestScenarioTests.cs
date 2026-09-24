using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

/// <summary>
///     Runs <see cref="RegisterMember" /> through Portia's real composed lifecycle to confirm
///     <see cref="RbacManagementAuthorizer" /> is actually wired to it via
///     <see cref="IRbacManagementRequest" />, not just correct in isolation.
/// </summary>
public sealed class RbacManagementRequestScenarioTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldAllowSystemActorGivenMissingMemberPermissions()
    {
        // Arrange
        await using var provider = RbacManagementServices.Build(allowed: false,
            portia => portia.AddRequestHandler<RegisterMemberHandler>());

        await RequestScenario.For(provider)
            .GivenActor(RequestActor.System)
            // Act
            .When(new RegisterMember(TenantId, Uuid.CreateVersion4()))
            // Assert
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectSuccess();
    }

    [Fact]
    public async Task ShouldDenyActorGivenMissingTenantRbacManagePermission()
    {
        // Arrange
        await using var provider = RbacManagementServices.Build(allowed: false,
            portia => portia.AddRequestHandler<RegisterMemberHandler>());

        await RequestScenario.For(provider)
            .GivenActor(RbacManagementServices.Actor())
            // Act
            .When(new RegisterMember(TenantId, Uuid.CreateVersion4()))
            // Assert
            .ExpectDenied(RequestErrorKind.Forbidden)
            .ExpectNotHandled();
    }

    [Fact]
    public async Task ShouldHandleRequestGivenTenantRbacManagePermission()
    {
        // Arrange
        await using var provider = RbacManagementServices.Build(allowed: true,
            portia => portia.AddRequestHandler<RegisterMemberHandler>());

        await RequestScenario.For(provider)
            .GivenActor(RbacManagementServices.Actor())
            // Act
            .When(new RegisterMember(TenantId, Uuid.CreateVersion4()))
            // Assert
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectSuccess();
    }
}
