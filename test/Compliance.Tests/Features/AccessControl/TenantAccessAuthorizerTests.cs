using System.Globalization;
using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class TenantAccessAuthorizerTests
{
    static readonly Uuid TenantId = Uuid.Parse("11f455d2-fb10-4f28-a157-23e18e706e70", CultureInfo.InvariantCulture);
    static readonly Uuid UserId = Uuid.Parse("0862062f-97e9-45de-a312-0f884c48180d", CultureInfo.InvariantCulture);

    [Fact]
    public async Task ShouldAllowSystemActorRegardlessOfPermissions()
    {
        var authorizer = new TenantAccessAuthorizer(new FakePermissionAuthorizer(false), new ActiveTenant());
        var request = new GetTeam(TenantId, Uuid.CreateVersion4());
        var context = new RequestContext<ITenantAccessRequest>(request, RequestActor.System);

        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectActorWithoutBdgrzIdentity()
    {
        var authorizer = new TenantAccessAuthorizer(new FakePermissionAuthorizer(true), new ActiveTenant());
        var request = new GetTeam(TenantId, Uuid.CreateVersion4());
        var context = new RequestContext<ITenantAccessRequest>(
            request,
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "https://issuer.example/"), new Claim("sub", "provider-subject")], "oidc")));

        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Unauthorized, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldAllowActorWithTenantAccessPermission()
    {
        var permissions = new FakePermissionAuthorizer(true);
        var authorizer = new TenantAccessAuthorizer(permissions, new ActiveTenant());
        var request = new GetTeam(TenantId, Uuid.CreateVersion4());
        var context = new RequestContext<ITenantAccessRequest>(request, BdgrzActor());

        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RbacIds.Member(TenantId, UserId), permissions.LastMemberId);
        Assert.Equal(RbacPermissions.TenantAccess, permissions.LastPermission);
    }

    [Fact]
    public async Task ShouldRejectActorWithoutTenantAccessPermission()
    {
        var authorizer = new TenantAccessAuthorizer(new FakePermissionAuthorizer(false), new ActiveTenant());
        var request = new GetTeam(TenantId, Uuid.CreateVersion4());
        var context = new RequestContext<ITenantAccessRequest>(request, BdgrzActor());

        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    static ClaimsPrincipal BdgrzActor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString())], "BdgrzSession"));

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
