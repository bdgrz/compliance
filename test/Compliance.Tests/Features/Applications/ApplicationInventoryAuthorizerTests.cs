using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Programs;
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
        var permissions = new Permissions(permitted);
        var authorizer = new ApplicationInventoryAuthorizer(
            new Memberships(member), new ActiveTenant(), permissions);
        var context = new RequestContext<IApplicationInventoryRequest>(
            new ListApplications(tenantId), BdgrzActor(userId));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(expected, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(member, permissions.Called);
    }

    [Fact]
    public async Task ShouldRequireInventoryPermissionGivenActiveTenantMember()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var permissions = new Permissions(true);
        var authorizer = new ApplicationInventoryAuthorizer(
            new Memberships(true), new ActiveTenant(), permissions);
        var context = new RequestContext<IApplicationInventoryRequest>(
            new PreviewApplicationChange(tenantId, Uuid.CreateVersion4(), 1, "retire"),
            BdgrzActor(userId));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(RbacPermissions.ApplicationInventoryManage, permissions.LastPermission);
        Assert.Equal(RbacIds.Member(tenantId, userId), permissions.LastMemberId);
    }

    [Fact]
    public async Task ShouldRequireProgramManagementGivenControlReferencesInApplicationPreview()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var permissions = new Permissions(false);
        var authorizer = new ProgramManagementAuthorizer(new Memberships(true), new ActiveTenant(),
            permissions);
        var context = new RequestContext<IProgramManagementRequest>(
            new PreviewApplicationChange(tenantId, Uuid.CreateVersion4(), 1, "retire"),
            BdgrzActor(userId));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(RbacPermissions.ProgramManage, permissions.LastPermission);
        Assert.Equal(RbacIds.Member(tenantId, userId), permissions.LastMemberId);
    }

    [Fact]
    public async Task ShouldDenyFirmStaffGivenHistoricalInventoryGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var permissions = new Permissions(true);
        var authorizer = new ApplicationInventoryAuthorizer(
            new Memberships(true, "firm_staff"), new ActiveTenant(), permissions);
        var context = new RequestContext<IApplicationInventoryRequest>(
            new ListApplications(tenantId), BdgrzActor(Uuid.CreateVersion4()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.False(permissions.Called);
    }

    static ClaimsPrincipal BdgrzActor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    sealed class Memberships(bool member, string affiliation = "client_personnel")
        : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult<TenantMembershipView?>(member
            ? new TenantMembershipView(userId,
                Uuid.Parse(tenantId, System.Globalization.CultureInfo.InvariantCulture), affiliation) : null);

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult(member);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(true);
    }

    sealed class Permissions(bool allowed) : IPermissionAuthorizer
    {
        public bool Called { get; private set; }
        public Uuid LastMemberId { get; private set; }
        public string? LastPermission { get; private set; }

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid memberId, string permission,
            CancellationToken ct = default)
        {
            Called = true;
            LastMemberId = memberId;
            LastPermission = permission;
            return ValueTask.FromResult(allowed);
        }
    }
}
