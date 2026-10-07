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
    [InlineData("stage", true)]
    [InlineData("get", true)]
    [InlineData("rows", true)]
    [InlineData("preview", true)]
    [InlineData("missing", true)]
    [InlineData("cancel", false)]
    [InlineData("correlate", false)]
    [InlineData("declare", false)]
    public async Task ShouldLimitImportStagingGrantGivenRequestedOperation(string operation,
        bool expectedAllowed)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var batchId = Uuid.CreateVersion4();
        IApplicationInventoryRequest request = operation switch
        {
            "stage" => new StageApplicationImport(tenantId, batchId, "manual", "applications",
                "partial", []),
            "get" => new GetApplicationImport(tenantId, batchId),
            "rows" => new ListApplicationImportRows(tenantId, batchId),
            "preview" => new PreviewApplicationImport(tenantId, batchId),
            "missing" => new PreviewMissingApplicationImportRows(tenantId, batchId),
            "cancel" => new CancelApplicationImport(tenantId, batchId, 1, "Canceled by lead"),
            "correlate" => new CorrelateApplicationImportRow(tenantId, batchId, Uuid.CreateVersion4(),
                1, "create_new", null, null, "Reviewed identity"),
            _ => new DeclareApplication(tenantId, "Payroll", "Run payroll"),
        };
        var permissions = new ImportStagingPermissions();
        var userId = Uuid.CreateVersion4();
        var authorizer = new ApplicationInventoryAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), permissions, new FixedScopedPermissions(false));
        var context = new RequestContext<IApplicationInventoryRequest>(request,
            BdgrzActor(userId));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(expectedAllowed, result.IsSuccess);
        Assert.Equal(expectedAllowed ? 2 : 1, permissions.Checks.Count);
        Assert.All(permissions.Checks, check =>
        {
            Assert.Equal(tenantId, check.TenantId);
            Assert.Equal(userId, check.UserId);
            Assert.Equal(RbacIds.Member(tenantId, userId), check.MemberId);
        });
        if (!expectedAllowed)
            Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
    }

    sealed class ImportStagingPermissions : IPermissionAuthorizer
    {
        public List<(Uuid TenantId, Uuid UserId, Uuid MemberId)> Checks { get; } = [];

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default)
        {
            Checks.Add((tenantId, userId, memberId));
            return ValueTask.FromResult(permission == "application_import.stage");
        }
    }

    [Theory]
    [InlineData(false, false, "client", RequestErrorKind.NotFound)]
    [InlineData(true, true, "client", RequestErrorKind.NotFound)]
    [InlineData(true, false, "firm_staff", RequestErrorKind.Forbidden)]
    public async Task ShouldDenyImportStagingGivenIneligibleMembership(bool member,
        bool suspended, string affiliation, RequestErrorKind expected)
    {
        // Arrange
        var permissions = new ImportStagingPermissions();
        var authorizer = new ApplicationInventoryAuthorizer(
            new FixedMembershipDirectory(member, affiliation, isSuspended: suspended),
            new ActiveTenant(), permissions, new FixedScopedPermissions(false));
        var context = new RequestContext<IApplicationInventoryRequest>(
            new PreviewMissingApplicationImportRows(Uuid.CreateVersion4(), Uuid.CreateVersion4()),
            BdgrzActor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(expected, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Checks);
    }

    [Theory]
    [InlineData(true, true, RequestErrorKind.NotFound)]
    [InlineData(false, false, RequestErrorKind.Forbidden)]
    public async Task ShouldDenyImportGrantGivenInactiveTenantOrDeprovisionedMember(
        bool tenantActive, bool deprovisioned, RequestErrorKind expected)
    {
        // Arrange
        var permissions = new ImportStagingPermissions();
        var authorizer = new ApplicationInventoryAuthorizer(
            new FixedMembershipDirectory(true, isDeprovisioned: deprovisioned),
            new ImportTenantActivity(tenantActive), permissions, new FixedScopedPermissions(false));
        var context = new RequestContext<IApplicationInventoryRequest>(
            new PreviewMissingApplicationImportRows(Uuid.CreateVersion4(), Uuid.CreateVersion4()),
            BdgrzActor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(expected, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(permissions.Checks);
    }

    sealed class ImportTenantActivity(bool active) : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(active);
    }

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
            new PreviewMissingApplicationImportRows(tenantId, Uuid.CreateVersion4()),
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
