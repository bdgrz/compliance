using System.Globalization;
using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class RbacManagementAuthorizerTests
{
    static readonly Uuid TenantId = Uuid.Parse("11f455d2-fb10-4f28-a157-23e18e706e70", CultureInfo.InvariantCulture);
    static readonly Uuid UserId = Uuid.Parse("0862062f-97e9-45de-a312-0f884c48180d", CultureInfo.InvariantCulture);

    [Fact]
    public async Task ShouldAllowSystemActorGivenMissingMemberPermission()
    {
        // Arrange
        var authorizer = new RbacManagementAuthorizer(new RecordingPermissionAuthorizer(false), new ActiveTenant(), new FixedMembershipDirectory(true));
        var request = new DefineTeam(TenantId, Uuid.CreateVersion4(), "Reviewers");
        var context = new RequestContext<IRbacManagementRequest>(request, RequestActor.System);

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectActorGivenMissingBdgrzIdentity()
    {
        // Arrange
        var authorizer = new RbacManagementAuthorizer(new RecordingPermissionAuthorizer(true), new ActiveTenant(), new FixedMembershipDirectory(true));
        var request = new DefineTeam(TenantId, Uuid.CreateVersion4(), "Reviewers");
        var context = new RequestContext<IRbacManagementRequest>(
            request,
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "https://issuer.example/"), new Claim("sub", "provider-subject")], "oidc")));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Unauthorized, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldAllowActorGivenTenantRbacManagePermission()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new RbacManagementAuthorizer(permissions, new ActiveTenant(), new FixedMembershipDirectory(true));
        var request = new DefineTeam(TenantId, Uuid.CreateVersion4(), "Reviewers");
        var context = new RequestContext<IRbacManagementRequest>(request, BdgrzActor());

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(RbacIds.Member(TenantId, UserId), Assert.Single(permissions.MemberIds));
        Assert.Equal(RbacPermissions.TenantRbacManage, Assert.Single(permissions.Permissions));
    }

    [Fact]
    public async Task ShouldRejectActorGivenMissingTenantRbacManagePermission()
    {
        // Arrange
        var authorizer = new RbacManagementAuthorizer(new RecordingPermissionAuthorizer(false), new ActiveTenant(), new FixedMembershipDirectory(true));
        var request = new DefineTeam(TenantId, Uuid.CreateVersion4(), "Reviewers");
        var context = new RequestContext<IRbacManagementRequest>(request, BdgrzActor());

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldDenyFirmStaffGivenHistoricalRbacManageGrant()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new RbacManagementAuthorizer(permissions, new ActiveTenant(),
            new FixedMembershipDirectory(true, "firm_staff"));
        var context = new RequestContext<IRbacManagementRequest>(
            new DefineTeam(TenantId, Uuid.CreateVersion4(), "Reviewers"), BdgrzActor());

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
        Assert.Empty(permissions.Permissions);
    }

    static ClaimsPrincipal BdgrzActor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString())], "BdgrzSession"));

    [Fact]
    public async Task ShouldHideTenantGivenNonmemberActor()
    {
        // Arrange
        var authorizer = new RbacManagementAuthorizer(new RecordingPermissionAuthorizer(true), new ActiveTenant(), new FixedMembershipDirectory(false));
        var context = new RequestContext<IRbacManagementRequest>(
            new DefineTeam(TenantId, Uuid.CreateVersion4(), "Reviewers"), BdgrzActor());

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
    }
}
