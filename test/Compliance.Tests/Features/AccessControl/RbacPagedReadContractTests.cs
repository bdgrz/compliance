using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class RbacPagedReadContractTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ShouldRejectOutOfRangeLimitsGivenRbacPagedReads(int limit)
    {
        // Arrange
        var readers = CreateReaders();

        // Act
        var teams = await new ListTeamsHandler(readers.Teams).HandleAsync(
            Context(new ListTeams(TenantId, limit)), CancellationToken.None);
        var members = await new ListTeamMembersHandler(readers.TeamMembers).HandleAsync(
            Context(new ListTeamMembers(TenantId, TeamId, limit)), CancellationToken.None);
        var roles = await new ListRolesHandler(readers.Roles).HandleAsync(
            Context(new ListRoles(TenantId, limit)), CancellationToken.None);
        var permissions = await new ListRolePermissionsHandler(readers.RolePermissions).HandleAsync(
            Context(new ListRolePermissions(TenantId, RoleId, limit)), CancellationToken.None);
        var roleTeams = await new ListRoleTeamsHandler(readers.RoleTeams).HandleAsync(
            Context(new ListRoleTeams(TenantId, RoleId, limit)), CancellationToken.None);

        // Assert
        AssertValidation(teams.Error);
        AssertValidation(members.Error);
        AssertValidation(roles.Error);
        AssertValidation(permissions.Error);
        AssertValidation(roleTeams.Error);
        Assert.Equal(0, readers.Teams.ListCalls);
        Assert.Equal(0, readers.TeamMembers.ListCalls);
        Assert.Equal(0, readers.Roles.ListCalls);
        Assert.Equal(0, readers.RolePermissions.ListCalls);
        Assert.Equal(0, readers.RoleTeams.ListCalls);
    }

    [Fact]
    public async Task ShouldTranslateInvalidCursorsGivenRbacPagedReads()
    {
        // Arrange
        var readers = CreateReaders(rejectCursor: true);

        // Act
        var teams = await new ListTeamsHandler(readers.Teams).HandleAsync(
            Context(new ListTeams(TenantId, Cursor: "invalid")), CancellationToken.None);
        var members = await new ListTeamMembersHandler(readers.TeamMembers).HandleAsync(
            Context(new ListTeamMembers(TenantId, TeamId, Cursor: "invalid")), CancellationToken.None);
        var roles = await new ListRolesHandler(readers.Roles).HandleAsync(
            Context(new ListRoles(TenantId, Cursor: "invalid")), CancellationToken.None);
        var permissions = await new ListRolePermissionsHandler(readers.RolePermissions).HandleAsync(
            Context(new ListRolePermissions(TenantId, RoleId, Cursor: "invalid")), CancellationToken.None);
        var roleTeams = await new ListRoleTeamsHandler(readers.RoleTeams).HandleAsync(
            Context(new ListRoleTeams(TenantId, RoleId, Cursor: "invalid")), CancellationToken.None);

        // Assert
        AssertValidation(teams.Error);
        AssertValidation(members.Error);
        AssertValidation(roles.Error);
        AssertValidation(permissions.Error);
        AssertValidation(roleTeams.Error);
    }

    [Fact]
    public async Task ShouldDefaultAndNormalizeQueriesGivenRbacPagedReads()
    {
        // Arrange
        var readers = CreateReaders();

        // Act
        _ = await new ListTeamsHandler(readers.Teams).HandleAsync(Context(
            new ListTeams(TenantId, Cursor: "teams", Search: "  review  ", Sort: "name:desc")),
            CancellationToken.None);
        _ = await new ListTeamMembersHandler(readers.TeamMembers).HandleAsync(Context(
            new ListTeamMembers(TenantId, TeamId, Cursor: "members", Search: "  member  ",
                Sort: "member_id:desc")), CancellationToken.None);
        _ = await new ListRolesHandler(readers.Roles).HandleAsync(Context(
            new ListRoles(TenantId, Cursor: "roles", Search: "  reviewer  ", Sort: "name:desc")),
            CancellationToken.None);
        _ = await new ListRolePermissionsHandler(readers.RolePermissions).HandleAsync(Context(
            new ListRolePermissions(TenantId, RoleId, Cursor: "permissions", Search: "  access  ",
                Sort: "permission:desc")), CancellationToken.None);
        _ = await new ListRoleTeamsHandler(readers.RoleTeams).HandleAsync(Context(
            new ListRoleTeams(TenantId, RoleId, Cursor: "role-teams", Search: "  team  ",
                Sort: "team_id:desc")), CancellationToken.None);

        // Assert
        AssertQuery(readers.Teams.LastLimit, readers.Teams.LastCursor, readers.Teams.LastSearch,
            readers.Teams.LastDescending, "teams", "review");
        AssertQuery(readers.TeamMembers.LastLimit, readers.TeamMembers.LastCursor,
            readers.TeamMembers.LastSearch, readers.TeamMembers.LastDescending, "members", "member");
        Assert.Equal(TeamId, readers.TeamMembers.LastTeamId);
        AssertQuery(readers.Roles.LastLimit, readers.Roles.LastCursor, readers.Roles.LastSearch,
            readers.Roles.LastDescending, "roles", "reviewer");
        AssertQuery(readers.RolePermissions.LastLimit, readers.RolePermissions.LastCursor,
            readers.RolePermissions.LastSearch, readers.RolePermissions.LastDescending, "permissions", "access");
        Assert.Equal(RoleId, readers.RolePermissions.LastRoleId);
        AssertQuery(readers.RoleTeams.LastLimit, readers.RoleTeams.LastCursor,
            readers.RoleTeams.LastSearch, readers.RoleTeams.LastDescending, "role-teams", "team");
        Assert.Equal(RoleId, readers.RoleTeams.LastRoleId);
    }

    [Fact]
    public async Task ShouldHideMismatchedParentEdgesGivenRbacPagedReads()
    {
        // Arrange
        var readers = CreateReaders();
        readers.TeamMembers.Page = new Page<TeamMemberView>([
            new TeamMemberView(Uuid.CreateVersion4(), Uuid.CreateVersion4()),
        ], null);
        readers.RolePermissions.Page = new Page<RolePermissionView>([
            new RolePermissionView(Uuid.CreateVersion4(), "controls.read"),
        ], null);
        readers.RoleTeams.Page = new Page<RoleTeamView>([
            new RoleTeamView(Uuid.CreateVersion4(), Uuid.CreateVersion4()),
        ], null);

        // Act
        var members = await new ListTeamMembersHandler(readers.TeamMembers).HandleAsync(
            Context(new ListTeamMembers(TenantId, TeamId)), CancellationToken.None);
        var permissions = await new ListRolePermissionsHandler(readers.RolePermissions).HandleAsync(
            Context(new ListRolePermissions(TenantId, RoleId)), CancellationToken.None);
        var roleTeams = await new ListRoleTeamsHandler(readers.RoleTeams).HandleAsync(
            Context(new ListRoleTeams(TenantId, RoleId)), CancellationToken.None);

        // Assert
        AssertError(members.Error, RequestErrorKind.NotFound);
        AssertError(permissions.Error, RequestErrorKind.NotFound);
        AssertError(roleTeams.Error, RequestErrorKind.NotFound);
    }

    static Readers CreateReaders(bool rejectCursor = false) => new(
        new TeamDirectoryReader { RejectCursor = rejectCursor },
        new TeamMemberDirectoryReader { RejectCursor = rejectCursor },
        new RoleDirectoryReader { RejectCursor = rejectCursor },
        new RolePermissionDirectoryReader { RejectCursor = rejectCursor },
        new RoleTeamDirectoryReader { RejectCursor = rejectCursor });

    static RequestContext<T> Context<T>(T request) where T : IRequestBase =>
        new(request, new ClaimsPrincipal());

    static void AssertValidation(RequestError? error) =>
        AssertError(error, RequestErrorKind.Validation);

    static void AssertError(RequestError? error, RequestErrorKind kind) =>
        Assert.Equal(kind, Assert.IsType<RequestError>(error).Kind);

    static void AssertQuery(int? limit, string? cursor, string? search, bool descending,
        string expectedCursor, string expectedSearch)
    {
        Assert.Equal(50, limit);
        Assert.Equal(expectedCursor, cursor);
        Assert.Equal(expectedSearch, search);
        Assert.True(descending);
    }

    sealed record Readers(
        TeamDirectoryReader Teams,
        TeamMemberDirectoryReader TeamMembers,
        RoleDirectoryReader Roles,
        RolePermissionDirectoryReader RolePermissions,
        RoleTeamDirectoryReader RoleTeams);

    sealed class TeamDirectoryReader : ITeamDirectoryReader
    {
        public bool RejectCursor { get; init; }
        public int ListCalls { get; private set; }
        public int? LastLimit { get; private set; }
        public string? LastCursor { get; private set; }
        public string? LastSearch { get; private set; }
        public bool LastDescending { get; private set; }
        public Page<TeamView> Page { get; set; } = new([], null);

        public ValueTask<TeamView?> GetAsync(Uuid tenantId, Uuid teamId,
            CancellationToken ct = default) => ValueTask.FromResult<TeamView?>(null);

        public ValueTask<Page<TeamView>> ListAsync(Uuid tenantId, int? limit, string? cursor,
            string? search, bool descending, CancellationToken ct = default)
        {
            ListCalls++;
            LastLimit = limit;
            LastCursor = cursor;
            LastSearch = search;
            LastDescending = descending;
            return RejectCursor
                ? ValueTask.FromException<Page<TeamView>>(new KvDirectoryQueryException())
                : ValueTask.FromResult(Page);
        }
    }

    sealed class TeamMemberDirectoryReader : ITeamMemberDirectoryReader
    {
        public bool RejectCursor { get; init; }
        public int ListCalls { get; private set; }
        public int? LastLimit { get; private set; }
        public string? LastCursor { get; private set; }
        public string? LastSearch { get; private set; }
        public bool LastDescending { get; private set; }
        public Uuid LastTeamId { get; private set; }
        public Page<TeamMemberView> Page { get; set; } = new([], null);

        public ValueTask<Page<TeamMemberView>> ListAsync(Uuid tenantId, Uuid teamId,
            int? limit, string? cursor, string? search, bool descending,
            CancellationToken ct = default)
        {
            ListCalls++;
            LastLimit = limit;
            LastCursor = cursor;
            LastSearch = search;
            LastDescending = descending;
            LastTeamId = teamId;
            return RejectCursor
                ? ValueTask.FromException<Page<TeamMemberView>>(new KvDirectoryQueryException())
                : ValueTask.FromResult(Page);
        }
    }

    sealed class RoleDirectoryReader : IRoleDirectoryReader
    {
        public bool RejectCursor { get; init; }
        public int ListCalls { get; private set; }
        public int? LastLimit { get; private set; }
        public string? LastCursor { get; private set; }
        public string? LastSearch { get; private set; }
        public bool LastDescending { get; private set; }
        public Page<RoleView> Page { get; set; } = new([], null);

        public ValueTask<RoleView?> GetAsync(Uuid tenantId, Uuid roleId,
            CancellationToken ct = default) => ValueTask.FromResult<RoleView?>(null);

        public ValueTask<Page<RoleView>> ListAsync(Uuid tenantId, int? limit, string? cursor,
            string? search, bool descending, CancellationToken ct = default)
        {
            ListCalls++;
            LastLimit = limit;
            LastCursor = cursor;
            LastSearch = search;
            LastDescending = descending;
            return RejectCursor
                ? ValueTask.FromException<Page<RoleView>>(new KvDirectoryQueryException())
                : ValueTask.FromResult(Page);
        }
    }

    sealed class RolePermissionDirectoryReader : IRolePermissionDirectoryReader
    {
        public bool RejectCursor { get; init; }
        public int ListCalls { get; private set; }
        public int? LastLimit { get; private set; }
        public string? LastCursor { get; private set; }
        public string? LastSearch { get; private set; }
        public bool LastDescending { get; private set; }
        public Uuid LastRoleId { get; private set; }
        public Page<RolePermissionView> Page { get; set; } = new([], null);

        public ValueTask<Page<RolePermissionView>> ListAsync(Uuid tenantId, Uuid roleId,
            int? limit, string? cursor, string? search, bool descending,
            CancellationToken ct = default)
        {
            ListCalls++;
            LastLimit = limit;
            LastCursor = cursor;
            LastSearch = search;
            LastDescending = descending;
            LastRoleId = roleId;
            return RejectCursor
                ? ValueTask.FromException<Page<RolePermissionView>>(new KvDirectoryQueryException())
                : ValueTask.FromResult(Page);
        }
    }

    sealed class RoleTeamDirectoryReader : IRoleTeamDirectoryReader
    {
        public bool RejectCursor { get; init; }
        public int ListCalls { get; private set; }
        public int? LastLimit { get; private set; }
        public string? LastCursor { get; private set; }
        public string? LastSearch { get; private set; }
        public bool LastDescending { get; private set; }
        public Uuid LastRoleId { get; private set; }
        public Page<RoleTeamView> Page { get; set; } = new([], null);

        public ValueTask<Page<RoleTeamView>> ListAsync(Uuid tenantId, Uuid roleId,
            int? limit, string? cursor, string? search, bool descending,
            CancellationToken ct = default)
        {
            ListCalls++;
            LastLimit = limit;
            LastCursor = cursor;
            LastSearch = search;
            LastDescending = descending;
            LastRoleId = roleId;
            return RejectCursor
                ? ValueTask.FromException<Page<RoleTeamView>>(new KvDirectoryQueryException())
                : ValueTask.FromResult(Page);
        }
    }
}
