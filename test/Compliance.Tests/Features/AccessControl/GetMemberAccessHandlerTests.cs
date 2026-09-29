using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class GetMemberAccessHandlerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid UserId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = RbacIds.Member(TenantId, UserId);
    static readonly Uuid TeamId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldExplainActiveGrantAndRetainExpiredHistoryGivenMemberAccess()
    {
        // Arrange
        var active = Grant(new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId),
            Now.AddDays(-1), null);
        var expired = Grant(new AccessGrantScope(AccessGrantScopeKind.Program, Uuid.CreateVersion4()),
            Now.AddDays(-3), Now.AddDays(-1));
        var handler = Handler("client_personnel", [active, expired]);
        var context = new RequestContext<GetMemberAccess>(new GetMemberAccess(TenantId, UserId),
            RequestActor.System);

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.GrantPaths.Count);
        Assert.Contains(result.Value.GrantPaths, path => path.Grant.GrantId == active.GrantId &&
            path.IsEffective && path.Permissions.Contains(RbacPermissions.ProgramManage));
        Assert.Contains(result.Value.GrantPaths, path => path.Grant.GrantId == expired.GrantId &&
            !path.IsEffective && path.Grant.Terms.Source.Id == "request-123");
        Assert.Contains(RbacPermissions.ProgramManage, result.Value.EffectivePermissions);
    }

    [Fact]
    public async Task ShouldNotTreatActiveGrantAsEffectiveGivenFirmStaffMembership()
    {
        // Arrange
        var active = Grant(new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId),
            Now.AddDays(-1), null);
        var handler = Handler("firm_staff", [active]);
        var context = new RequestContext<GetMemberAccess>(new GetMemberAccess(TenantId, UserId),
            RequestActor.System);

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(Assert.Single(result.Value.GrantPaths).IsEffective);
        Assert.Empty(result.Value.EffectivePermissions);
    }

    [Fact]
    public async Task ShouldExplainTeamGrantGivenCurrentTeamMember()
    {
        // Arrange
        var grant = Grant(new AccessGrantScope(AccessGrantScopeKind.Program, Uuid.CreateVersion4()),
            Now.AddDays(-1), null);
        var teamGrant = grant with
        {
            Terms = grant.Terms with
            {
                Principal = new AccessGrantPrincipal(AccessGrantPrincipalKind.Team, TeamId),
            },
        };
        var handler = new GetMemberAccessHandler(new MembershipDirectory("client_personnel"),
            new EmptyAccessReader(), new TeamDirectory(TeamId), new RoleDirectory(),
            new GrantDirectory([teamGrant]), new TeamMemberDirectory(MemberId, TeamId),
            new RolePermissionDirectory(), new FixedTimeProvider(Now));
        var context = new RequestContext<GetMemberAccess>(new GetMemberAccess(TenantId, UserId),
            RequestActor.System);

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var path = Assert.Single(result.Value.GrantPaths);
        Assert.True(path.IsEffective);
        Assert.Equal(AccessGrantPrincipalKind.Team, path.Grant.Terms.Principal.Kind);
        Assert.Equal(TeamId, path.Grant.Terms.Principal.Id);
    }

    [Fact]
    public async Task ShouldMarkGrantIneffectiveGivenRevocationProjectsBetweenListAndPendingScan()
    {
        // Arrange
        var grant = Grant(new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId),
            Now.AddDays(-1), null);
        var directory = new GrantDirectory(grant) { ProjectRevocationOnPendingScan = true };
        var handler = new GetMemberAccessHandler(new MembershipDirectory("client_personnel"),
            new EmptyAccessReader(), new TeamDirectory(), new RoleDirectory(), directory,
            new TeamMemberDirectory(), new RolePermissionDirectory(), new FixedTimeProvider(Now));
        var context = new RequestContext<GetMemberAccess>(new GetMemberAccess(TenantId, UserId),
            RequestActor.System);

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(Assert.Single(result.Value.GrantPaths).IsEffective);
        Assert.DoesNotContain(RbacPermissions.ProgramManage, result.Value.EffectivePermissions);
        Assert.True(directory.ListCount >= 2);
    }

    [Fact]
    public async Task ShouldHideTeamGrantGivenRemovalProjectsBetweenListAndPendingScan()
    {
        // Arrange
        var grant = Grant(new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId),
            Now.AddDays(-1), null);
        var teamGrant = grant with
        {
            Terms = grant.Terms with
            {
                Principal = new AccessGrantPrincipal(AccessGrantPrincipalKind.Team, TeamId),
            },
        };
        var members = new TeamMemberDirectory(MemberId, TeamId)
        {
            ProjectRemovalOnPendingScan = true,
        };
        var handler = new GetMemberAccessHandler(new MembershipDirectory("client_personnel"),
            new EmptyAccessReader(), new TeamDirectory(TeamId), new RoleDirectory(),
            new GrantDirectory(teamGrant), members, new RolePermissionDirectory(),
            new FixedTimeProvider(Now));
        var context = new RequestContext<GetMemberAccess>(new GetMemberAccess(TenantId, UserId),
            RequestActor.System);

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.GrantPaths);
        Assert.DoesNotContain(RbacPermissions.ProgramManage, result.Value.EffectivePermissions);
        Assert.True(members.ListCount >= 2);
    }

    static GetMemberAccessHandler Handler(string affiliation, params AccessGrantView[] grants) =>
        new(new MembershipDirectory(affiliation), new EmptyAccessReader(), new TeamDirectory(),
            new RoleDirectory(), new GrantDirectory(grants), new TeamMemberDirectory(),
            new RolePermissionDirectory(), new FixedTimeProvider(Now));

    static AccessGrantView Grant(AccessGrantScope scope, DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveUntil) => new(TenantId, Uuid.CreateVersion4(),
        new AccessGrantTerms(new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, MemberId),
            RoleId, scope, new AccessGrantSource("manual", "request-123"),
            ActorReference.ForMember(MemberId, "Organization Admin"), effectiveFrom, effectiveUntil),
        null, null);

    sealed class MembershipDirectory(string affiliation) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult<TenantMembershipView?>(
            tenantId == TenantId.ToString() && userId == UserId
                ? new TenantMembershipView(UserId, TenantId, affiliation)
                : null);

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == TenantId.ToString() && userId == UserId);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class EmptyAccessReader : IMemberAccessReader
    {
        public ValueTask<IReadOnlyList<MemberAccessEdge>> ReadAsync(Uuid tenantId, Uuid memberId,
            CancellationToken ct = default) => ValueTask.FromResult<IReadOnlyList<MemberAccessEdge>>([]);
    }

    sealed class TeamDirectory(params Uuid[] teamIds) : ITeamDirectoryReader
    {
        public ValueTask<TeamView?> GetAsync(Uuid tenantId, Uuid teamId, CancellationToken ct = default) =>
            ValueTask.FromResult<TeamView?>(teamIds.Contains(teamId)
                ? new TeamView(teamId, "Reviewers")
                : null);

        public ValueTask<Page<TeamView>> ListAsync(Uuid tenantId, int? limit, string? cursor,
            string? search, bool descending, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TeamView>(teamIds.Select(teamId =>
                new TeamView(teamId, "Reviewers")).ToArray(), null));
    }

    sealed class RoleDirectory : IRoleDirectoryReader
    {
        public ValueTask<RoleView?> GetAsync(Uuid tenantId, Uuid roleId, CancellationToken ct = default) =>
            ValueTask.FromResult<RoleView?>(roleId == RoleId ? new RoleView(RoleId, "Program Admin") : null);

        public ValueTask<Page<RoleView>> ListAsync(Uuid tenantId, int? limit, string? cursor,
            string? search, bool descending, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<RoleView>([], null));
    }

    sealed class GrantDirectory(params AccessGrantView[] grants) : IAccessGrantDirectory
    {
        public bool ProjectRevocationOnPendingScan { get; init; }
        public int ListCount { get; private set; }
        bool _revocationProjected;

        public ValueTask<AccessGrantSetView> ListAsync(Uuid tenantId, CancellationToken ct = default)
        {
            ListCount++;
            var current = _revocationProjected
                ? grants.Select(grant => grant with
                {
                    RevokedAt = Now.AddMinutes(-1),
                    RevokedBy = ActorReference.ForMember(MemberId, "Organization Admin"),
                }).ToArray()
                : grants;
            return ValueTask.FromResult(new AccessGrantSetView(tenantId, 1, current));
        }

        public ValueTask<AccessGrantView?> GetAsync(Uuid tenantId, Uuid grantId,
            CancellationToken ct = default) => ValueTask.FromResult<AccessGrantView?>(
            grants.SingleOrDefault(grant => grant.TenantId == tenantId && grant.GrantId == grantId));

        public ValueTask<IReadOnlySet<Uuid>> FindPendingRevocationsAsync(Uuid tenantId,
            IReadOnlySet<Uuid> grantIds, CancellationToken ct = default)
        {
            if (ProjectRevocationOnPendingScan)
                _revocationProjected = true;
            return ValueTask.FromResult<IReadOnlySet<Uuid>>(new HashSet<Uuid>());
        }
    }

    sealed class TeamMemberDirectory(Uuid? memberId = null, params Uuid[] teamIds)
        : ITeamMemberDirectoryReader
    {
        public bool ProjectRemovalOnPendingScan { get; init; }
        public int ListCount { get; private set; }
        bool _removalProjected;

        public ValueTask<Page<TeamMemberView>> ListAsync(Uuid tenantId, Uuid teamId, int? limit,
            string? cursor, string? search, bool descending, CancellationToken ct = default)
        {
            ListCount++;
            return ValueTask.FromResult(new Page<TeamMemberView>(
                !_removalProjected && teamIds.Contains(teamId) && memberId is { } id
                    ? [new TeamMemberView(teamId, id)]
                    : [], null));
        }

        public ValueTask<bool> HasPendingRemovalAsync(Uuid tenantId, Uuid teamId, Uuid memberId,
            CancellationToken ct = default)
        {
            if (ProjectRemovalOnPendingScan)
                _removalProjected = true;
            return ValueTask.FromResult(false);
        }
    }

    sealed class RolePermissionDirectory : IRolePermissionDirectoryReader
    {
        public ValueTask<Page<RolePermissionView>> ListAsync(Uuid tenantId, Uuid roleId,
            int? limit, string? cursor, string? search, bool descending, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<RolePermissionView>(
                roleId == RoleId ? [new RolePermissionView(RoleId, RbacPermissions.ProgramManage)] : [], null));
    }

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
