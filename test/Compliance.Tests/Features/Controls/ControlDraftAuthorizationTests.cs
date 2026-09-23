using System.Security.Claims;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlDraftAuthorizationTests
{
    [Fact]
    public async Task ShouldDenyDraftReadGivenTenantMemberWithoutProgramManage()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
            "BdgrzSession"));
        var permissions = new DeniedPermission();
        var authorizer = new ProgramManagementAuthorizer(new Memberships(),
            new ActiveTenant(), permissions);
        var context = new RequestContext<IProgramManagementRequest>(
            new GetControlDraft(tenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4()), actor);

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(RbacPermissions.ProgramManage, permissions.LastPermission);
    }

    sealed class Memberships : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult<TenantMembershipView?>(
            new TenantMembershipView(userId,
                Uuid.Parse(tenantId, System.Globalization.CultureInfo.InvariantCulture)));

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult(true);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(true);
    }

    sealed class DeniedPermission : IPermissionAuthorizer
    {
        public string? LastPermission { get; private set; }

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default)
        {
            LastPermission = permission;
            return ValueTask.FromResult(false);
        }
    }
}
