using System.Globalization;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class SeparationOfDutiesWaiverAuthorizerTests
{
    static readonly Uuid TenantId = Uuid.Parse("11f455d2-fb10-4f28-a157-23e18e706e70",
        CultureInfo.InvariantCulture);
    static readonly Uuid UserId = Uuid.Parse("0862062f-97e9-45de-a312-0f884c48180d",
        CultureInfo.InvariantCulture);
    static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldAllowClientOrgAdminGivenActiveTenantMembershipAndManagePermission()
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new SeparationOfDutiesWaiverAuthorizer(
            new FixedMembershipDirectory(true), new ActiveTenant(), permissions);
        ISeparationOfDutiesWaiverAdminRequest request = new GetSeparationOfDutiesWaiver(
            TenantId, Uuid.CreateVersion4());
        var context = new RequestContext<ISeparationOfDutiesWaiverAdminRequest>(request,
            BdgrzActor());

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(RbacPermissions.TenantRbacManage, Assert.Single(permissions.Permissions));
    }

    [Fact]
    public async Task ShouldRejectMemberGivenMissingTenantManagePermission()
    {
        // Arrange
        var authorizer = new SeparationOfDutiesWaiverAuthorizer(
            new FixedMembershipDirectory(true), new ActiveTenant(),
            new RecordingPermissionAuthorizer(false));
        ISeparationOfDutiesWaiverAdminRequest request = new RecordSeparationOfDutiesWaiver(
            TenantId, new SeparationOfDutiesWaiverScope("boundary", Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), 1, "review"), Uuid.CreateVersion4(),
            "No alternate reviewer is available.", Now.AddDays(1));
        var context = new RequestContext<ISeparationOfDutiesWaiverAdminRequest>(request,
            BdgrzActor());

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldRejectFirmStaffGivenHistoricalTenantManageGrant()
    {
        // Arrange
        var authorizer = new SeparationOfDutiesWaiverAuthorizer(
            new FixedMembershipDirectory(true, "firm_staff"), new ActiveTenant(),
            new RecordingPermissionAuthorizer(true));
        var context = new RequestContext<ISeparationOfDutiesWaiverAdminRequest>(
            new ApproveSeparationOfDutiesWaiver(TenantId, Uuid.CreateVersion4()), BdgrzActor());

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    static ClaimsPrincipal BdgrzActor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString())], "BdgrzSession"));
}
