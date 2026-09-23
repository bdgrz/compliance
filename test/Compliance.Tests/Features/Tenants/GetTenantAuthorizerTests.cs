using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class GetTenantAuthorizerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid UserId = Uuid.CreateVersion4();

    [Theory]
    [InlineData(true, true, true, null)]
    [InlineData(false, true, true, RequestErrorKind.NotFound)]
    [InlineData(true, false, true, RequestErrorKind.Forbidden)]
    [InlineData(true, true, false, RequestErrorKind.Forbidden)]
    public async Task ShouldRequireMembershipActivityAndPermissionGivenMemberAccess(
        bool member, bool active, bool permitted, RequestErrorKind? expectedError)
    {
        // Arrange
        var authorizer = new GetTenantAuthorizer(new PlatformOperatorAuthority([]),
            new Memberships(member), new TenantActivity(active), new Permissions(permitted));

        // Act
        var result = await authorizer.AuthorizeAsync(
            new RequestContext<GetTenant>(new GetTenant(TenantId), Actor()), CancellationToken.None);

        // Assert
        if (expectedError is null)
            Assert.True(result.IsSuccess);
        else
        {
            Assert.False(result.IsSuccess);
            Assert.Equal(expectedError, result.Error.Kind);
        }
    }

    [Fact]
    public async Task ShouldAllowOperatorReadGivenNoTenantMembership()
    {
        // Arrange
        var authorizer = new GetTenantAuthorizer(new PlatformOperatorAuthority([UserId]),
            new Memberships(false), new TenantActivity(false), new Permissions(false));

        // Act
        var result = await authorizer.AuthorizeAsync(
            new RequestContext<GetTenant>(new GetTenant(TenantId), Actor()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString())], "BdgrzSession"));

    sealed class Memberships(bool member) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult<TenantMembershipView?>(member
            ? new TenantMembershipView(userId, TenantId) : null);

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(member);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class TenantActivity(bool active) : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(active);
    }

    sealed class Permissions(bool permitted) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(permitted);
    }
}
