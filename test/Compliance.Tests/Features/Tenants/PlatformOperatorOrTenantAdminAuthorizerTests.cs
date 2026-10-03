using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class PlatformOperatorOrTenantAdminAuthorizerTests
{
    [Fact]
    public async Task ShouldAllowOrgAdminGivenActiveTenantRbacManage()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(allowed: true);
        var authorizer = Authorizer(new FixedMembershipDirectory(true), permissions);

        // Act
        var result = await authorizer.AuthorizeAsync(Context(tenantId, userId),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(RbacPermissions.TenantRbacManage,
            Assert.Single(permissions.Permissions));
        Assert.Equal(RbacIds.Member(tenantId, userId), Assert.Single(permissions.MemberIds));
    }

    [Fact]
    public async Task ShouldDenyTenantMemberGivenMissingTenantRbacManage()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(allowed: false);
        var authorizer = Authorizer(new FixedMembershipDirectory(true), permissions);

        // Act
        var result = await authorizer.AuthorizeAsync(Context(Uuid.CreateVersion4(),
            Uuid.CreateVersion4()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(RbacPermissions.TenantRbacManage,
            Assert.Single(permissions.Permissions));
    }

    [Fact]
    public async Task ShouldForbidCallerWithoutTargetTenantMembershipGivenSlugChange()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(allowed: true);
        var authorizer = Authorizer(new FixedMembershipDirectory(member: false), permissions);

        // Act
        var result = await authorizer.AuthorizeAsync(Context(Uuid.CreateVersion4(),
            Uuid.CreateVersion4()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Permissions);
    }

    [Fact]
    public async Task ShouldDenyFirmStaffGivenTenantRbacManagePermission()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(allowed: true);
        var authorizer = Authorizer(new FixedMembershipDirectory(true, "firm_staff"), permissions);

        // Act
        var result = await authorizer.AuthorizeAsync(Context(Uuid.CreateVersion4(),
            Uuid.CreateVersion4()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Permissions);
    }

    [Fact]
    public async Task ShouldHideSuspendedTenantGivenHistoricalAdminGrant()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(allowed: true);
        var authorizer = Authorizer(new FixedMembershipDirectory(true, isSuspended: true),
            permissions);

        // Act
        var result = await authorizer.AuthorizeAsync(Context(Uuid.CreateVersion4(),
            Uuid.CreateVersion4()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Permissions);
    }

    [Fact]
    public async Task ShouldAllowPlatformOperatorGivenNoTenantMembership()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(allowed: false);
        var authorizer = new PlatformOperatorOrTenantAdminAuthorizer(
            new FixedOperatorAccess(userId), new FixedMembershipDirectory(member: false),
            new ActiveTenant(), permissions);

        // Act
        var result = await authorizer.AuthorizeAsync(Context(Uuid.CreateVersion4(), userId),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(permissions.Permissions);
    }

    [Fact]
    public async Task ShouldRejectActorGivenMissingBdgrzIdentity()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(allowed: true);
        var authorizer = Authorizer(new FixedMembershipDirectory(true), permissions);
        var request = new ChangeTenantSlug(Uuid.CreateVersion4(), "acme-next");
        var context = new RequestContext<IPlatformOperatorOrTenantAdminRequest>(request,
            new ClaimsPrincipal(new ClaimsIdentity()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Unauthorized, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Permissions);
    }

    static PlatformOperatorOrTenantAdminAuthorizer Authorizer(
        ITenantMembershipDirectoryReader memberships, IPermissionAuthorizer permissions) =>
        new(new FixedOperatorAccess(), memberships, new ActiveTenant(), permissions);

    static RequestContext<IPlatformOperatorOrTenantAdminRequest> Context(Uuid tenantId,
        Uuid userId) => new(new ChangeTenantSlug(tenantId, "acme-next"), Actor(userId));

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));
}
