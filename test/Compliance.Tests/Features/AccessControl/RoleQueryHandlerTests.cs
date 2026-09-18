using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class RoleQueryHandlerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();

    [Fact]
    public async Task GetRoleShouldReturnTheRoleGivenItExists()
    {
        var reader = new FakeRoleDirectoryReader();
        reader.Roles[(TenantId, RoleId)] = new RoleView(RoleId, "Reviewer");
        var handler = new GetRoleHandler(reader);
        var context = new RequestContext<GetRole>(new GetRole(TenantId, RoleId), Actor());

        var result = await handler.HandleAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new RoleView(RoleId, "Reviewer"), result.Value);
    }

    [Fact]
    public async Task GetRoleShouldReturnNotFoundGivenNoSuchRole()
    {
        var handler = new GetRoleHandler(new FakeRoleDirectoryReader());
        var context = new RequestContext<GetRole>(new GetRole(TenantId, RoleId), Actor());

        var result = await handler.HandleAsync(context, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    public async Task ListRolesShouldReturnThePageFromTheReader()
    {
        var reader = new FakeRoleDirectoryReader();
        reader.Roles[(TenantId, RoleId)] = new RoleView(RoleId, "Reviewer");
        var handler = new ListRolesHandler(reader);
        var context = new RequestContext<ListRoles>(new ListRoles(TenantId), Actor());

        var result = await handler.HandleAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var role = Assert.Single(result.Value.Items);
        Assert.Equal(RoleId, role.RoleId);
        Assert.Null(result.Value.NextCursor);
    }

    [Fact]
    public async Task ListRolesShouldBuildANormalizedQueryFromTheRequest()
    {
        var reader = new FakeRoleDirectoryReader();
        var handler = new ListRolesHandler(reader);
        var context = new RequestContext<ListRoles>(
            new ListRoles(TenantId, Limit: 5, Cursor: "opaque", Search: "  review  ", Sort: "name:desc"),
            Actor());

        _ = await handler.HandleAsync(context, CancellationToken.None);

        Assert.Equal(5, reader.LastLimit);
        Assert.Equal("opaque", reader.LastCursor);
        Assert.Equal("review", reader.LastSearch);
        Assert.True(reader.LastDescending);
    }

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

    sealed class FakeRoleDirectoryReader : IRoleDirectoryReader
    {
        public Dictionary<(Uuid TenantId, Uuid RoleId), RoleView> Roles { get; } = [];
        public int? LastLimit { get; private set; }
        public string? LastCursor { get; private set; }
        public string? LastSearch { get; private set; }
        public bool LastDescending { get; private set; }

        public ValueTask<RoleView?> GetAsync(Uuid tenantId, Uuid roleId, CancellationToken ct = default) =>
            ValueTask.FromResult(Roles.TryGetValue((tenantId, roleId), out var role) ? role : null);

        public ValueTask<Page<RoleView>> ListAsync(
            Uuid tenantId,
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
            IReadOnlyList<RoleView> items =
                [.. Roles.Where(entry => entry.Key.TenantId == tenantId).Select(entry => entry.Value)
                    .Take(limit ?? 50)];
            return ValueTask.FromResult(new Page<RoleView>(items, null));
        }
    }
}
