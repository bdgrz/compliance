using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ListTeamRolesHandlerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReturnPageGivenReaderResult()
    {
        // Arrange
        var reader = new FakeTeamRoleDirectoryReader();
        reader.Roles[(TenantId, TeamId)] = [new TeamRoleView(TeamId, RoleId)];
        var handler = new ListTeamRolesHandler(reader);
        var context = new RequestContext<ListTeamRoles>(new ListTeamRoles(TenantId, TeamId), Actor());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(RoleId, Assert.Single(result.Value.Items).RoleId);
    }

    [Fact]
    public async Task ShouldBuildNormalizedQueryGivenRequest()
    {
        // Arrange
        var reader = new FakeTeamRoleDirectoryReader();
        var handler = new ListTeamRolesHandler(reader);
        var context = new RequestContext<ListTeamRoles>(
            new ListTeamRoles(TenantId, TeamId, Limit: 5, Cursor: "opaque", Search: "  abc  ", Sort: "role_id:desc"),
            Actor());

        // Act
        _ = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(5, reader.LastLimit);
        Assert.Equal("opaque", reader.LastCursor);
        Assert.Equal("abc", reader.LastSearch);
        Assert.True(reader.LastDescending);
        Assert.Equal(TeamId, reader.LastTeamId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ShouldRejectRequestGivenLimitOutOfRange(int limit)
    {
        // Arrange
        var handler = new ListTeamRolesHandler(new FakeTeamRoleDirectoryReader());
        var context = new RequestContext<ListTeamRoles>(new ListTeamRoles(TenantId, TeamId, Limit: limit), Actor());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenCursorFromAnotherTeam()
    {
        // Arrange
        var reader = new FakeTeamRoleDirectoryReader();
        reader.Roles[(TenantId, TeamId)] = [new TeamRoleView(Uuid.CreateVersion4(), RoleId)];
        var handler = new ListTeamRolesHandler(reader);
        var context = new RequestContext<ListTeamRoles>(new ListTeamRoles(TenantId, TeamId), Actor());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
    }

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

    sealed class FakeTeamRoleDirectoryReader : ITeamRoleDirectoryReader
    {
        public Dictionary<(Uuid TenantId, Uuid TeamId), List<TeamRoleView>> Roles { get; } = [];
        public int? LastLimit { get; private set; }
        public string? LastCursor { get; private set; }
        public string? LastSearch { get; private set; }
        public bool LastDescending { get; private set; }
        public Uuid LastTeamId { get; private set; }

        public ValueTask<Page<TeamRoleView>> ListAsync(
            Uuid tenantId,
            Uuid teamId,
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
            LastTeamId = teamId;
            IReadOnlyList<TeamRoleView> items = Roles.TryGetValue((tenantId, teamId), out var found)
                ? [.. found.Take(limit ?? 50)]
                : [];
            return ValueTask.FromResult(new Page<TeamRoleView>(items, null));
        }
    }
}
