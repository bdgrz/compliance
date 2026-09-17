using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class TeamQueryHandlerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();

    [Fact]
    public async Task GetTeamShouldReturnTheTeamGivenItExists()
    {
        var reader = new FakeTeamDirectoryReader();
        reader.Teams[(TenantId, TeamId)] = new TeamView(TeamId, "Reviewers");
        var handler = new GetTeamHandler(reader);
        var context = new RequestContext<GetTeam>(new GetTeam(TenantId, TeamId), Actor());

        var result = await handler.HandleAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new TeamView(TeamId, "Reviewers"), result.Value);
    }

    [Fact]
    public async Task GetTeamShouldReturnNotFoundGivenNoSuchTeam()
    {
        var handler = new GetTeamHandler(new FakeTeamDirectoryReader());
        var context = new RequestContext<GetTeam>(new GetTeam(TenantId, TeamId), Actor());

        var result = await handler.HandleAsync(context, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    public async Task ListTeamsShouldReturnThePageFromTheReader()
    {
        var reader = new FakeTeamDirectoryReader();
        reader.Teams[(TenantId, TeamId)] = new TeamView(TeamId, "Reviewers");
        var handler = new ListTeamsHandler(reader);
        var context = new RequestContext<ListTeams>(new ListTeams(TenantId), Actor());

        var result = await handler.HandleAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var team = Assert.Single(result.Value.Items);
        Assert.Equal(TeamId, team.TeamId);
        Assert.Null(result.Value.NextCursor);
    }

    [Fact]
    public async Task ListTeamsShouldBuildANormalizedQueryFromTheRequest()
    {
        var reader = new FakeTeamDirectoryReader();
        var handler = new ListTeamsHandler(reader);
        var context = new RequestContext<ListTeams>(
            new ListTeams(TenantId, Limit: 5, Cursor: "opaque", Search: "  review  ", Sort: "name:desc"),
            Actor());

        _ = await handler.HandleAsync(context, CancellationToken.None);

        Assert.Equal(5, reader.LastLimit);
        Assert.Equal("opaque", reader.LastCursor);
        Assert.Equal("review", reader.LastSearch);
        Assert.True(reader.LastDescending);
    }

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

    sealed class FakeTeamDirectoryReader : ITeamDirectoryReader
    {
        public Dictionary<(Uuid TenantId, Uuid TeamId), TeamView> Teams { get; } = [];
        public int? LastLimit { get; private set; }
        public string? LastCursor { get; private set; }
        public string? LastSearch { get; private set; }
        public bool LastDescending { get; private set; }

        public ValueTask<TeamView?> GetAsync(Uuid tenantId, Uuid teamId, CancellationToken ct = default) =>
            ValueTask.FromResult(Teams.TryGetValue((tenantId, teamId), out var team) ? team : null);

        public ValueTask<Page<TeamView>> ListAsync(
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
            IReadOnlyList<TeamView> items =
                [.. Teams.Where(entry => entry.Key.TenantId == tenantId).Select(entry => entry.Value)
                    .Take(limit ?? 50)];
            return ValueTask.FromResult(new Page<TeamView>(items, null));
        }
    }
}
