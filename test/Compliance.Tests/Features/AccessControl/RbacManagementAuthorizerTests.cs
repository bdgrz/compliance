using System.Globalization;
using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class RbacManagementAuthorizerTests
{
    static readonly Uuid TenantId = Uuid.Parse("11f455d2-fb10-4f28-a157-23e18e706e70", CultureInfo.InvariantCulture);
    static readonly Uuid UserId = Uuid.Parse("0862062f-97e9-45de-a312-0f884c48180d", CultureInfo.InvariantCulture);

    [Fact]
    public async Task ShouldAllowSystemActorRegardlessOfPermissions()
    {
        var authorizer = new RbacManagementAuthorizer(new FakePermissionAuthorizer(false), new ActiveTenant(), new Memberships(true));
        var request = new DefineTeam(TenantId, Uuid.CreateVersion4(), "Reviewers");
        var context = new RequestContext<IRbacManagementRequest>(request, RequestActor.System);

        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectActorWithoutBdgrzIdentity()
    {
        var authorizer = new RbacManagementAuthorizer(new FakePermissionAuthorizer(true), new ActiveTenant(), new Memberships(true));
        var request = new DefineTeam(TenantId, Uuid.CreateVersion4(), "Reviewers");
        var context = new RequestContext<IRbacManagementRequest>(
            request,
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "https://issuer.example/"), new Claim("sub", "provider-subject")], "oidc")));

        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Unauthorized, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldAllowActorWithTenantRbacManagePermission()
    {
        var permissions = new FakePermissionAuthorizer(true);
        var authorizer = new RbacManagementAuthorizer(permissions, new ActiveTenant(), new Memberships(true));
        var request = new DefineTeam(TenantId, Uuid.CreateVersion4(), "Reviewers");
        var context = new RequestContext<IRbacManagementRequest>(request, BdgrzActor());

        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RbacIds.Member(TenantId, UserId), permissions.LastMemberId);
        Assert.Equal(RbacPermissions.TenantRbacManage, permissions.LastPermission);
    }

    [Fact]
    public async Task ShouldRejectActorWithoutTenantRbacManagePermission()
    {
        var authorizer = new RbacManagementAuthorizer(new FakePermissionAuthorizer(false), new ActiveTenant(), new Memberships(true));
        var request = new DefineTeam(TenantId, Uuid.CreateVersion4(), "Reviewers");
        var context = new RequestContext<IRbacManagementRequest>(request, BdgrzActor());

        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    static ClaimsPrincipal BdgrzActor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString())], "BdgrzSession"));

    [Fact]
    public async Task ShouldHideTenantFromNonmember()
    {
        var authorizer = new RbacManagementAuthorizer(new FakePermissionAuthorizer(true), new ActiveTenant(), new Memberships(false));
        var context = new RequestContext<IRbacManagementRequest>(
            new DefineTeam(TenantId, Uuid.CreateVersion4(), "Reviewers"), BdgrzActor());

        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
    }

    sealed class Memberships(bool member) : ITenantMembershipDirectoryReader
    {
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(member);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(true);
    }

    sealed class FakePermissionAuthorizer(bool allowed) : IPermissionAuthorizer
    {
        public Uuid LastMemberId { get; private set; }
        public string? LastPermission { get; private set; }

        public ValueTask<bool> IsAllowedAsync(
            Uuid tenantId,
            Uuid memberId,
            string permission,
            CancellationToken ct = default)
        {
            LastMemberId = memberId;
            LastPermission = permission;
            return ValueTask.FromResult(allowed);
        }
    }
}
