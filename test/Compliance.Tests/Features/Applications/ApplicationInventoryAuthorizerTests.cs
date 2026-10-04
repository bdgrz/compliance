using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
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
            new FixedMembershipDirectory(member), new ActiveTenant(), permissions,
            new FixedScopedPermissions(false));
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
            new FixedMembershipDirectory(true), new ActiveTenant(), permissions,
            new FixedScopedPermissions(false));
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
    public async Task ShouldAllowReadGivenActiveMemberWithRestrictedReadScopeOnly()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(false);
        var scopedPermissions = new FixedScopedPermissions(true);
        var authorizer = new ApplicationInventoryAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), permissions, scopedPermissions);
        var context = new RequestContext<IApplicationInventoryRequest>(
            new ListApplications(tenantId), BdgrzActor(userId));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(RbacPermissions.ApplicationInventoryManage,
            Assert.Single(permissions.Permissions));
        Assert.Equal(1, scopedPermissions.InventoryScopeChecks);
    }

    [Fact]
    public async Task ShouldDenyImportReadGivenRestrictedReadScopeOnly()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(false);
        var scopedPermissions = new FixedScopedPermissions(true);
        var authorizer = new ApplicationInventoryAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), permissions, scopedPermissions);
        var context = new RequestContext<IApplicationInventoryRequest>(
            new GetApplicationImport(tenantId, Uuid.CreateVersion4()),
            BdgrzActor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(0, scopedPermissions.InventoryScopeChecks);
    }

    [Fact]
    public async Task ShouldDenyWriteGivenRestrictedReadScopeOnly()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(false);
        var scopedPermissions = new FixedScopedPermissions(true);
        var authorizer = new ApplicationInventoryAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), permissions, scopedPermissions);
        var context = new RequestContext<IApplicationInventoryRequest>(new DeclareApplication(
            tenantId, "Payroll", "Run payroll"), BdgrzActor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(RbacPermissions.ApplicationInventoryManage,
            Assert.Single(permissions.Permissions));
        Assert.Equal(0, scopedPermissions.InventoryScopeChecks);
    }

    [Fact]
    public async Task ShouldRequireProgramManagementGivenControlReferencesInApplicationPreview()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(false);
        var authorizer = new ProgramManagementAuthorizer(new FixedMembershipDirectory(true), new ActiveTenant(),
            new PermissionBackedAccessGrantPermissionAuthorizer(permissions),
            ProgramManagementServices.ResourceScopes());
        var context = new RequestContext<IProgramManagementRequest>(
            new PreviewApplicationChange(tenantId, Uuid.CreateVersion4(), 1, "retire"),
            BdgrzActor(userId));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(["tenant.access"], permissions.Permissions);
    }

    [Fact]
    public async Task ShouldHideMissingApplicationGivenPreviewWithoutOrganizationGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(false);
        var authorizer = new ProgramManagementAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), new PermissionBackedAccessGrantPermissionAuthorizer(permissions),
            ProgramManagementServices.ResourceScopes(applicationExists: false));
        var context = new RequestContext<IProgramManagementRequest>(
            new PreviewApplicationChange(tenantId, Uuid.CreateVersion4(), 1, "retire"),
            BdgrzActor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Permissions);
    }

    [Fact]
    public async Task ShouldDenyFirmStaffGivenHistoricalInventoryGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new ApplicationInventoryAuthorizer(
            new FixedMembershipDirectory(true, "firm_staff"), new ActiveTenant(), permissions,
            new FixedScopedPermissions(false));
        var context = new RequestContext<IApplicationInventoryRequest>(
            new ListApplications(tenantId), BdgrzActor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Permissions);
    }

    [Fact]
    public async Task ShouldDenySuspendedMemberGivenHistoricalInventoryGrant()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new ApplicationInventoryAuthorizer(
            new FixedMembershipDirectory(true, isSuspended: true), new ActiveTenant(), permissions,
            new FixedScopedPermissions(false));
        var context = new RequestContext<IApplicationInventoryRequest>(
            new ListApplications(Uuid.CreateVersion4()), BdgrzActor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Permissions);
    }

    [Fact]
    public async Task ShouldDenySuspendedMemberGivenHistoricalProgramManagementGrant()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new ProgramManagementAuthorizer(
            new FixedMembershipDirectory(true, isSuspended: true), new ActiveTenant(),
            new PermissionBackedAccessGrantPermissionAuthorizer(permissions),
            ProgramManagementServices.ResourceScopes());
        var context = new RequestContext<IProgramManagementRequest>(
            new PreviewApplicationChange(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, "retire"),
            BdgrzActor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Permissions);
    }

    [Theory]
    [InlineData(false, RequestErrorKind.Forbidden)]
    [InlineData(true, null)]
    public async Task ShouldRequireOrganizationProgramManagementGivenAccessReviewScopeDecision(
        bool permitted, RequestErrorKind? expected)
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(permitted);
        var authorizer = new ProgramManagementAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), new PermissionBackedAccessGrantPermissionAuthorizer(permissions),
            ProgramManagementServices.ResourceScopes());
        var context = new RequestContext<IProgramManagementRequest>(
            new DecideAccessReviewScope(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), 1, 0, "included", "Production data",
                DateTimeOffset.UtcNow), BdgrzActor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(expected, (result.Error as RequestError)?.Kind);
        Assert.Contains("program.manage", permissions.Permissions);
    }

    [Fact]
    public async Task ShouldHideMissingApplicationGivenAccessReviewScopeDecision()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new ProgramManagementAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), new PermissionBackedAccessGrantPermissionAuthorizer(permissions),
            ProgramManagementServices.ResourceScopes(applicationExists: false));
        var context = new RequestContext<IProgramManagementRequest>(
            new DecideAccessReviewScope(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), 1, 0, "excluded", "Sandbox only",
                DateTimeOffset.UtcNow), BdgrzActor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Permissions);
    }

    static ClaimsPrincipal BdgrzActor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    sealed class FixedScopedPermissions(bool allowed) : IAccessGrantScopePermissionAuthorizer
    {
        public int InventoryScopeChecks { get; private set; }

        public ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, IReadOnlyCollection<AccessGrantScope> scopes, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(false);

        public ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default)
        {
            InventoryScopeChecks++;
            return ValueTask.FromResult(allowed);
        }
    }
}
