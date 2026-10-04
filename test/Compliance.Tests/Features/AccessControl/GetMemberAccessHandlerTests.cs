using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Tests.Testing;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRetainGrantHistoryWithoutEffectivePermissionsGivenPendingSuspension(
        bool teamGrant)
    {
        // Arrange
        var grant = Grant(new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId),
            Now.AddDays(-1), null);
        if (teamGrant)
            grant = grant with
            {
                Terms = grant.Terms with
                {
                    Principal = new AccessGrantPrincipal(AccessGrantPrincipalKind.Team, TeamId),
                },
            };
        var handler = new GetMemberAccessHandler(new MembershipDirectory("client_personnel"),
            new StandingAccessReader(new MemberAccessEdge(TeamId, RoleId,
                [RbacPermissions.ProgramManage])), new TeamDirectory(TeamId), new RoleDirectory(),
            new GrantDirectory(grant), new TeamMemberDirectory(MemberId, TeamId),
            new RolePermissionDirectory(), new RbacSourceReader(), new FixedTimeProvider(Now),
            new FixedMemberAccessEligibility(false));

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetMemberAccess>(
            new GetMemberAccess(TenantId, UserId), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Paths);
        var grantPath = Assert.Single(result.Value.GrantPaths);
        Assert.Equal(grant.GrantId, grantPath.Grant.GrantId);
        Assert.False(grantPath.IsEffective);
        Assert.Empty(result.Value.EffectivePermissions);
    }

    [Fact]
    public async Task ShouldClearGrantAndStandingPermissionsGivenSuspensionDuringReadback()
    {
        // Arrange
        var eligibility = new MutableMemberAccessEligibility();
        var grant = Grant(new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId),
            Now.AddDays(-1), null);
        var grants = new GrantDirectory(grant) { OnPendingScan = () => eligibility.IsEligible = false };
        var handler = new GetMemberAccessHandler(new MembershipDirectory("client_personnel"),
            new StandingAccessReader(new MemberAccessEdge(TeamId, RoleId,
                [RbacPermissions.ProgramManage])), new TeamDirectory(TeamId), new RoleDirectory(),
            grants, new TeamMemberDirectory(MemberId, TeamId), new RolePermissionDirectory(),
            new RbacSourceReader(), new FixedTimeProvider(Now), eligibility);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetMemberAccess>(
            new GetMemberAccess(TenantId, UserId), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Paths);
        Assert.False(Assert.Single(result.Value.GrantPaths).IsEffective);
        Assert.Empty(result.Value.EffectivePermissions);
        Assert.Equal(2, eligibility.ReadCount);
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
            new RolePermissionDirectory(), new RbacSourceReader(), new FixedTimeProvider(Now), new FixedMemberAccessEligibility(true));
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
    public async Task ShouldKeepOldMemberGrantIneffectiveGivenFreshMembershipEpisode()
    {
        // Arrange
        var currentEpisodeId = Uuid.CreateVersion4();
        var oldGrant = Grant(new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId),
            Now.AddDays(-1), null);
        var directory = new GrantDirectory(oldGrant);
        var handler = new GetMemberAccessHandler(new MembershipDirectory("client_personnel"),
            new EmptyAccessReader(), new TeamDirectory(), new RoleDirectory(),
            directory, new TeamMemberDirectory(), new RolePermissionDirectory(),
            new RbacSourceReader(), new FixedTimeProvider(Now), new FixedMemberAccessEligibility(true)
            {
                MembershipEpisodeId = currentEpisodeId,
            });

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetMemberAccess>(
            new GetMemberAccess(TenantId, UserId), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(Assert.Single(result.Value.GrantPaths).IsEffective);
        Assert.Empty(result.Value.EffectivePermissions);
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
            new TeamMemberDirectory(), new RolePermissionDirectory(), new RbacSourceReader(),
            new FixedTimeProvider(Now), new FixedMemberAccessEligibility(true));
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
            new RbacSourceReader(), new FixedTimeProvider(Now), new FixedMemberAccessEligibility(true));
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

    [Fact]
    public async Task ShouldHideTeamGrantGivenRemovalProjectsDuringGrantScan()
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
        var members = new TeamMemberDirectory(MemberId, TeamId);
        var grants = new GrantDirectory(teamGrant) { OnPendingScan = members.ProjectRemoval };
        var handler = new GetMemberAccessHandler(new MembershipDirectory("client_personnel"),
            new EmptyAccessReader(), new TeamDirectory(TeamId), new RoleDirectory(), grants,
            members, new RolePermissionDirectory(), new RbacSourceReader(),
            new FixedTimeProvider(Now), new FixedMemberAccessEligibility(true));
        var context = new RequestContext<GetMemberAccess>(new GetMemberAccess(TenantId, UserId),
            RequestActor.System);

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.GrantPaths);
        Assert.DoesNotContain(RbacPermissions.ProgramManage, result.Value.EffectivePermissions);
    }

    [Fact]
    public async Task ShouldMarkGrantIneffectiveGivenRolePermissionRemovalNotProjected()
    {
        // Arrange
        var grant = Grant(new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId),
            Now.AddDays(-1), null);
        var handler = Handler("client_personnel", new RbacSourceReader
        {
            PermissionRemoved = true,
        }, grant);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetMemberAccess>(
            new GetMemberAccess(TenantId, UserId), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var path = Assert.Single(result.Value.GrantPaths);
        Assert.Equal(grant.GrantId, path.Grant.GrantId);
        Assert.False(path.IsEffective);
        Assert.Empty(path.Permissions);
        Assert.Empty(result.Value.EffectivePermissions);
    }

    [Fact]
    public async Task ShouldMarkGrantIneffectiveGivenRoleDeletedBeforeCleanup()
    {
        // Arrange
        var grant = Grant(new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId),
            Now.AddDays(-1), null);
        var handler = Handler("client_personnel", new RbacSourceReader
        {
            RoleDeleted = true,
        }, grant);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetMemberAccess>(
            new GetMemberAccess(TenantId, UserId), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(Assert.Single(result.Value.GrantPaths).IsEffective);
        Assert.Empty(result.Value.EffectivePermissions);
    }

    [Fact]
    public async Task ShouldMarkTeamGrantIneffectiveGivenTeamDeletedBeforeCleanup()
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
        var handler = new GetMemberAccessHandler(new MembershipDirectory("client_personnel"),
            new EmptyAccessReader(), new TeamDirectory(TeamId), new RoleDirectory(),
            new GrantDirectory(teamGrant), new TeamMemberDirectory(MemberId, TeamId),
            new RolePermissionDirectory(), new RbacSourceReader { TeamDeleted = true },
            new FixedTimeProvider(Now), new FixedMemberAccessEligibility(true));

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetMemberAccess>(
            new GetMemberAccess(TenantId, UserId), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(teamGrant.GrantId, Assert.Single(result.Value.GrantPaths).Grant.GrantId);
        Assert.False(Assert.Single(result.Value.GrantPaths).IsEffective);
        Assert.Empty(result.Value.EffectivePermissions);
    }

    [Theory]
    [InlineData("role_permission")]
    [InlineData("role")]
    [InlineData("team")]
    [InlineData("team_member")]
    [InlineData("team_role")]
    public async Task ShouldPreserveStandingPathWithoutPermissionGivenSourceRevocation(
        string revokedSource)
    {
        // Arrange
        var source = new RbacSourceReader
        {
            PermissionRemoved = revokedSource == "role_permission",
            RoleDeleted = revokedSource == "role",
            TeamDeleted = revokedSource == "team",
            TeamMemberRemoved = revokedSource == "team_member",
            TeamRoleRemoved = revokedSource == "team_role",
        };
        var edge = new MemberAccessEdge(TeamId, RoleId, [RbacPermissions.ProgramManage]);
        var handler = new GetMemberAccessHandler(new MembershipDirectory("client_personnel"),
            new StandingAccessReader(edge), new TeamDirectory(TeamId), new RoleDirectory(),
            new GrantDirectory(), new TeamMemberDirectory(MemberId, TeamId),
            new RolePermissionDirectory(), source, new FixedTimeProvider(Now), new FixedMemberAccessEligibility(true));

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetMemberAccess>(
            new GetMemberAccess(TenantId, UserId), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(RbacPermissions.ProgramManage, Assert.Single(result.Value.Paths).Permissions);
        Assert.Empty(result.Value.EffectivePermissions);
    }

    [Fact]
    public async Task ShouldIncludeStandingPermissionGivenActiveSourceRelationships()
    {
        // Arrange
        var edge = new MemberAccessEdge(TeamId, RoleId, [RbacPermissions.ProgramManage]);
        var handler = new GetMemberAccessHandler(new MembershipDirectory("client_personnel"),
            new StandingAccessReader(edge), new TeamDirectory(TeamId), new RoleDirectory(),
            new GrantDirectory(), new TeamMemberDirectory(MemberId, TeamId),
            new RolePermissionDirectory(), new RbacSourceReader(), new FixedTimeProvider(Now), new FixedMemberAccessEligibility(true));

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetMemberAccess>(
            new GetMemberAccess(TenantId, UserId), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(RbacPermissions.ProgramManage, result.Value.EffectivePermissions);
    }

    static GetMemberAccessHandler Handler(string affiliation, params AccessGrantView[] grants) =>
        Handler(affiliation, new RbacSourceReader(), grants);

    static GetMemberAccessHandler Handler(string affiliation, RbacSourceReader source,
        params AccessGrantView[] grants) =>
        new(new MembershipDirectory(affiliation), new EmptyAccessReader(), new TeamDirectory(),
            new RoleDirectory(), new GrantDirectory(grants), new TeamMemberDirectory(),
            new RolePermissionDirectory(), source, new FixedTimeProvider(Now), new FixedMemberAccessEligibility(true));

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

    sealed class StandingAccessReader(params MemberAccessEdge[] edges) : IMemberAccessReader
    {
        public ValueTask<IReadOnlyList<MemberAccessEdge>> ReadAsync(Uuid tenantId, Uuid memberId,
            CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<MemberAccessEdge>>(edges);
    }

    sealed class MutableMemberAccessEligibility : IMemberAccessEligibility
    {
        public bool IsEligible { get; set; } = true;
        public int ReadCount { get; private set; }

        public ValueTask<bool> IsEligibleAsync(Uuid tenantId, Uuid userId,
            CancellationToken ct = default)
        {
            ReadCount++;
            return ValueTask.FromResult(IsEligible);
        }
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
        public Uuid? MembershipEpisodeId { get; init; }
        public Action? OnPendingScan { get; init; }
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

        public ValueTask<Uuid?> GetMembershipEpisodeIdAsync(Uuid tenantId, Uuid grantId,
            CancellationToken ct = default) => ValueTask.FromResult(MembershipEpisodeId);

        public ValueTask<IReadOnlySet<Uuid>> FindPendingRevocationsAsync(Uuid tenantId,
            IReadOnlySet<Uuid> grantIds, CancellationToken ct = default)
        {
            OnPendingScan?.Invoke();
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

        public void ProjectRemoval() => _removalProjected = true;

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

    sealed class RbacSourceReader : IAggregateReader
    {
        public bool RoleDeleted { get; init; }
        public bool TeamDeleted { get; init; }
        public bool PermissionRemoved { get; init; }
        public bool TeamMemberRemoved { get; init; }
        public bool TeamRoleRemoved { get; init; }

        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            switch (aggregate)
            {
                case Role role:
                    Assert.True(role.Define("Program Admin").IsSuccess);
                    if (RoleDeleted)
                        Assert.True(role.Delete().IsSuccess);
                    break;
                case Team team:
                    Assert.True(team.Define("Reviewers").IsSuccess);
                    if (TeamDeleted)
                        Assert.True(team.Delete().IsSuccess);
                    break;
                case RolePermission permission:
                    Assert.True(permission.Assign().IsSuccess);
                    if (PermissionRemoved)
                        Assert.True(permission.Remove().IsSuccess);
                    break;
                case TeamMember member:
                    Assert.True(member.Assign().IsSuccess);
                    if (TeamMemberRemoved)
                        Assert.True(member.Remove().IsSuccess);
                    break;
                case TeamRole teamRole:
                    Assert.True(teamRole.Assign().IsSuccess);
                    if (TeamRoleRemoved)
                        Assert.True(teamRole.Remove().IsSuccess);
                    break;
            }
            return ValueTask.FromResult(aggregate);
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
