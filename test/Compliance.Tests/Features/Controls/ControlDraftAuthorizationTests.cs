using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlDraftAuthorizationTests
{
    [Fact]
    public async Task ShouldDenyDraftReadGivenTenantMemberWithoutProgramManage()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(allowed: false);
        await using var provider = ProgramManagementServices.Build(permissions,
            portia => portia.AddRequestHandler<GetControlDraftHandler>());

        await RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(Uuid.CreateVersion4()))
            // Act
            .When(new GetControlDraft(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                Uuid.CreateVersion4()))
            // Assert
            .ExpectDenied(RequestErrorKind.Forbidden)
            .ExpectNotHandled();
        Assert.Equal([RbacPermissions.ProgramManage], permissions.Permissions);
    }
}
