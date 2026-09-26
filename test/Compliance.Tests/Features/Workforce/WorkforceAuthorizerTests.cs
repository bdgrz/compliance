using System.Security.Claims;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Workforce;

public sealed class WorkforceAuthorizerTests
{
    [Theory]
    [InlineData(false, true, "client_personnel", RequestErrorKind.NotFound)]
    [InlineData(true, false, "client_personnel", RequestErrorKind.Forbidden)]
    [InlineData(true, true, "firm_staff", RequestErrorKind.Forbidden)]
    public async Task ShouldDenyRosterGivenMissingMembershipGrantOrEngagement(bool member,
        bool permitted, string affiliation, RequestErrorKind expected)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var authorizer = new WorkforceAuthorizer(new FixedMembershipDirectory(member, affiliation),
            new ActiveTenant(), new RecordingPermissionAuthorizer(permitted));
        var context = new RequestContext<IWorkforceRequest>(new ListPeople(tenantId),
            BdgrzActor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(expected, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldRequireWorkforcePermissionGivenActiveTenantMember()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new WorkforceAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), permissions);
        var context = new RequestContext<IWorkforceRequest>(
            new RecordPerson(tenantId, "Ada Lovelace"), BdgrzActor(userId));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(RbacPermissions.WorkforceManage, Assert.Single(permissions.Permissions));
        Assert.Equal(RbacIds.Member(tenantId, userId), Assert.Single(permissions.MemberIds));
    }

    static ClaimsPrincipal BdgrzActor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));
}
