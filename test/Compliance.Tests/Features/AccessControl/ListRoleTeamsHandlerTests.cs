using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ListRoleTeamsHandlerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReturnThePageFromTheReader()
    {
        var reader = new FakeRoleTeamDirectoryReader();
        reader.Teams[(TenantId, RoleId)] = [new RoleTeamView(RoleId, TeamId)];
        var handler = new ListRoleTeamsHandler(reader);
        var context = new RequestContext<ListRoleTeams>(new ListRoleTeams(TenantId, RoleId), Actor());

        var result = await handler.HandleAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var team = Assert.Single(result.Value.Items);
        Assert.Equal(TeamId, team.TeamId);
    }

    [Fact]
    public async Task ShouldBuildANormalizedQueryFromTheRequest()
    {
        var reader = new FakeRoleTeamDirectoryReader();
        var handler = new ListRoleTeamsHandler(reader);
        var context = new RequestContext<ListRoleTeams>(
            new ListRoleTeams(TenantId, RoleId, Limit: 5, Cursor: "opaque", Search: "  abc  ", Sort: "team_id:desc"),
            Actor());

        _ = await handler.HandleAsync(context, CancellationToken.None);

        Assert.Equal(5, reader.LastLimit);
        Assert.Equal("opaque", reader.LastCursor);
        Assert.Equal("abc", reader.LastSearch);
        Assert.True(reader.LastDescending);
        Assert.Equal(RoleId, reader.LastRoleId);
    }

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

    sealed class FakeRoleTeamDirectoryReader : IRoleTeamDirectoryReader
    {
        public Dictionary<(Uuid TenantId, Uuid RoleId), List<RoleTeamView>> Teams { get; } = [];
        public int? LastLimit { get; private set; }
        public string? LastCursor { get; private set; }
        public string? LastSearch { get; private set; }
        public bool LastDescending { get; private set; }
        public Uuid LastRoleId { get; private set; }

        public ValueTask<Page<RoleTeamView>> ListAsync(
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
            IReadOnlyList<RoleTeamView> items = Teams.TryGetValue((tenantId, roleId), out var items0)
                ? [.. items0.Take(limit ?? 50)]
                : [];
            return ValueTask.FromResult(new Page<RoleTeamView>(items, null));
        }
    }
}
