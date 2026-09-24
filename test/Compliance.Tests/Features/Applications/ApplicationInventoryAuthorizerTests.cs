using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationInventoryAuthorizerTests
{
    [Theory]
    [InlineData(false, true, RequestErrorKind.NotFound)]
    [InlineData(true, false, RequestErrorKind.Forbidden)]
    public async Task ShouldDenyInventoryGivenMissingMembershipOrGrant(bool member,
        bool permitted, RequestErrorKind expected)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(permitted);
        var authorizer = new ApplicationInventoryAuthorizer(
            new FixedMembershipDirectory(member), new ActiveTenant(), permissions);
        var context = new RequestContext<IApplicationInventoryRequest>(
            new ListApplications(tenantId), BdgrzActor(userId));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(expected, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(member ? [RbacPermissions.ApplicationInventoryManage] : [],
            permissions.Permissions);
    }

    [Fact]
    public async Task ShouldRequireInventoryPermissionGivenActiveTenantMember()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new ApplicationInventoryAuthorizer(
            new FixedMembershipDirectory(true), new ActiveTenant(), permissions);
        var context = new RequestContext<IApplicationInventoryRequest>(
            new PreviewApplicationChange(tenantId, Uuid.CreateVersion4(), 1, "retire"),
            BdgrzActor(userId));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(RbacPermissions.ApplicationInventoryManage, Assert.Single(permissions.Permissions));
        Assert.Equal(RbacIds.Member(tenantId, userId), Assert.Single(permissions.MemberIds));
    }

    [Fact]
    public async Task ShouldRequireProgramManagementGivenControlReferencesInApplicationPreview()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(false);
        var authorizer = new ProgramManagementAuthorizer(new FixedMembershipDirectory(true), new ActiveTenant(),
            permissions);
        var context = new RequestContext<IProgramManagementRequest>(
            new PreviewApplicationChange(tenantId, Uuid.CreateVersion4(), 1, "retire"),
            BdgrzActor(userId));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(RbacPermissions.ProgramManage, Assert.Single(permissions.Permissions));
        Assert.Equal(RbacIds.Member(tenantId, userId), Assert.Single(permissions.MemberIds));
    }

    [Fact]
    public async Task ShouldDenyFirmStaffGivenHistoricalInventoryGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new ApplicationInventoryAuthorizer(
            new FixedMembershipDirectory(true, "firm_staff"), new ActiveTenant(), permissions);
        var context = new RequestContext<IApplicationInventoryRequest>(
            new ListApplications(tenantId), BdgrzActor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Permissions);
    }

    static ClaimsPrincipal BdgrzActor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));
}
