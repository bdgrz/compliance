using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ListRolePermissionsHandlerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReturnPageGivenReaderResult()
    {
        // Arrange
        var reader = new FakeRolePermissionDirectoryReader();
        reader.Permissions[(TenantId, RoleId)] = [new RolePermissionView(RoleId, "controls.read")];
        var handler = new ListRolePermissionsHandler(reader);
        var context = new RequestContext<ListRolePermissions>(new ListRolePermissions(TenantId, RoleId), Actor());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var permission = Assert.Single(result.Value.Items);
        Assert.Equal("controls.read", permission.Permission);
    }

    [Fact]
    public async Task ShouldBuildNormalizedQueryGivenRequest()
    {
        // Arrange
        var reader = new FakeRolePermissionDirectoryReader();
        var handler = new ListRolePermissionsHandler(reader);
        var context = new RequestContext<ListRolePermissions>(
            new ListRolePermissions(TenantId, RoleId, Limit: 5, Cursor: "opaque", Search: "  abc  ", Sort: "permission:desc"),
            Actor());

        // Act
        _ = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(5, reader.LastLimit);
        Assert.Equal("opaque", reader.LastCursor);
        Assert.Equal("abc", reader.LastSearch);
        Assert.True(reader.LastDescending);
        Assert.Equal(RoleId, reader.LastRoleId);
    }

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

    sealed class FakeRolePermissionDirectoryReader : IRolePermissionDirectoryReader
    {
        public Dictionary<(Uuid TenantId, Uuid RoleId), List<RolePermissionView>> Permissions { get; } = [];
        public int? LastLimit { get; private set; }
        public string? LastCursor { get; private set; }
        public string? LastSearch { get; private set; }
        public bool LastDescending { get; private set; }
        public Uuid LastRoleId { get; private set; }

        public ValueTask<Page<RolePermissionView>> ListAsync(
            Uuid tenantId,
            Uuid roleId,
            int? limit,
            string? cursor,
            string? search,
            bool descending,
            CancellationToken ct = default)
        {
            LastLimit = limit;
            LastCursor = cursor;
            LastSearch = search;
            LastDescending = descending;
            LastRoleId = roleId;
            IReadOnlyList<RolePermissionView> items = Permissions.TryGetValue((tenantId, roleId), out var items0)
                ? [.. items0.Take(limit ?? 50)]
                : [];
            return ValueTask.FromResult(new Page<RolePermissionView>(items, null));
        }
    }
}
