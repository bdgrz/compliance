using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class AccessGrantPermissionAuthorizerTests
{
    static readonly Uuid TenantId = Uuid.Parse("11f455d2-fb10-4f28-a157-23e18e706e70", CultureInfo.InvariantCulture);
    static readonly Uuid UserId = Uuid.Parse("0862062f-97e9-45de-a312-0f884c48180d", CultureInfo.InvariantCulture);
    static readonly Uuid MemberId = RbacIds.Member(TenantId, UserId);
    static readonly Uuid RoleId = Uuid.Parse("5b66f817-415a-44e8-b809-503cf44b1537", CultureInfo.InvariantCulture);
    static readonly Uuid ProgramId = Uuid.Parse("e9a858f1-25e1-4594-87d2-814a73fc87ec", CultureInfo.InvariantCulture);
    static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldAllowMatchingProgramGrantGivenCurrentMemberAndRolePermission()
    {
        // Arrange
        var directory = new GrantDirectory(Grant(
            new AccessGrantScope(AccessGrantScopeKind.Program, ProgramId)));
        var authorizer = Authorizer(directory, "client_personnel", [RbacPermissions.ProgramManage]);

        // Act
        var allowed = await authorizer.IsAllowedAsync(TenantId, UserId, MemberId, ProgramId,
            RbacPermissions.ProgramManage);

        // Assert
        Assert.True(allowed);
    }

    [Fact]
    public async Task ShouldDenyDifferentProgramGrantGivenMatchingRolePermission()
    {
        // Arrange
        var directory = new GrantDirectory(Grant(new AccessGrantScope(
            AccessGrantScopeKind.Program, Uuid.CreateVersion4())));
        var authorizer = Authorizer(directory, "client_personnel", [RbacPermissions.ProgramManage]);

        // Act
        var allowed = await authorizer.IsAllowedAsync(TenantId, UserId, MemberId, ProgramId,
            RbacPermissions.ProgramManage);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public async Task ShouldDenyOrganizationTeamGrantGivenFirmStaffMember()
    {
        // Arrange
        var teamId = Uuid.CreateVersion4();
        var grant = Grant(new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId),
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Team, teamId));
        var authorizer = new AccessGrantPermissionAuthorizer(new GrantDirectory(grant),
            new MembershipDirectory("firm_staff"), new TeamMemberDirectory(MemberId),
            new RolePermissions([RbacPermissions.ProgramManage]), new FixedTimeProvider(Now));

        // Act
        var allowed = await authorizer.IsAllowedAsync(TenantId, UserId, MemberId, ProgramId,
            RbacPermissions.ProgramManage);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public async Task ShouldDenyTeamGrantGivenMembershipRemovalHasNotReachedProjection()
    {
        // Arrange
        var teamId = Uuid.CreateVersion4();
        var grant = Grant(new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId),
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Team, teamId));
        var teamMembers = new TeamMemberDirectory(MemberId) { PendingRemoval = true };
        var authorizer = new AccessGrantPermissionAuthorizer(new GrantDirectory(grant),
            new MembershipDirectory("client_personnel"), teamMembers,
            new RolePermissions([RbacPermissions.ProgramManage]), new FixedTimeProvider(Now));

        // Act
        var allowed = await authorizer.IsAllowedAsync(TenantId, UserId, MemberId, ProgramId,
            RbacPermissions.ProgramManage);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public async Task ShouldDenyRevokedGrantGivenPreviouslyAuthorizedMember()
    {
        // Arrange
        var grant = Grant(new AccessGrantScope(AccessGrantScopeKind.Program, ProgramId)) with
        {
            RevokedBy = ActorReference.ForMember(MemberId, "Organization Admin"),
            RevokedAt = Now.AddMinutes(-1),
        };
        var authorizer = Authorizer(new GrantDirectory(grant), "client_personnel",
            [RbacPermissions.ProgramManage]);

        // Act
        var allowed = await authorizer.IsAllowedAsync(TenantId, UserId, MemberId, ProgramId,
            RbacPermissions.ProgramManage);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public async Task ShouldDenyPendingRevocationGivenStaleGrantProjection()
    {
        // Arrange
        var grant = Grant(new AccessGrantScope(AccessGrantScopeKind.Program, ProgramId));
        var directory = new GrantDirectory(grant.GrantId, grant);
        var authorizer = Authorizer(directory, "client_personnel", [RbacPermissions.ProgramManage]);

        // Act
        var allowed = await authorizer.IsAllowedAsync(TenantId, UserId, MemberId, ProgramId,
            RbacPermissions.ProgramManage);

        // Assert
        Assert.False(allowed);
        Assert.Equal([grant.GrantId], directory.CheckedGrantIds);
    }

    [Fact]
    public async Task ShouldAllowRemainingGrantGivenAnotherGrantHasPendingRevocation()
    {
        // Arrange
        var revoked = Grant(new AccessGrantScope(AccessGrantScopeKind.Program, ProgramId));
        var active = Grant(new AccessGrantScope(AccessGrantScopeKind.Program, ProgramId));
        var directory = new GrantDirectory(revoked.GrantId, revoked, active);
        var authorizer = Authorizer(directory, "client_personnel", [RbacPermissions.ProgramManage]);

        // Act
        var allowed = await authorizer.IsAllowedAsync(TenantId, UserId, MemberId, ProgramId,
            RbacPermissions.ProgramManage);

        // Assert
        Assert.True(allowed);
    }

    [Fact]
    public async Task ShouldDenyGrantGivenRevocationProjectsBetweenListAndPendingScan()
    {
        // Arrange
        var grant = Grant(new AccessGrantScope(AccessGrantScopeKind.Program, ProgramId));
        var directory = new GrantDirectory(grant) { ProjectRevocationOnPendingScan = true };
        var authorizer = Authorizer(directory, "client_personnel", [RbacPermissions.ProgramManage]);

        // Act
        var allowed = await authorizer.IsAllowedAsync(TenantId, UserId, MemberId, ProgramId,
            RbacPermissions.ProgramManage);

        // Assert
        Assert.False(allowed);
        Assert.True(directory.ListCount >= 2);
    }

    [Fact]
    public async Task ShouldDenyTeamGrantGivenRemovalProjectsBetweenListAndPendingScan()
    {
        // Arrange
        var teamId = Uuid.CreateVersion4();
        var grant = Grant(new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId),
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Team, teamId));
        var teamMembers = new TeamMemberDirectory(MemberId) { ProjectRemovalOnPendingScan = true };
        var authorizer = new AccessGrantPermissionAuthorizer(new GrantDirectory(grant),
            new MembershipDirectory("client_personnel"), teamMembers,
            new RolePermissions([RbacPermissions.ProgramManage]), new FixedTimeProvider(Now));

        // Act
        var allowed = await authorizer.IsAllowedAsync(TenantId, UserId, MemberId, ProgramId,
            RbacPermissions.ProgramManage);

        // Assert
        Assert.False(allowed);
        Assert.True(teamMembers.ListCount >= 2);
    }

    static AccessGrantPermissionAuthorizer Authorizer(GrantDirectory grants, string affiliation,
        IReadOnlyList<string> permissions) => new(grants,
        new MembershipDirectory(affiliation), new TeamMemberDirectory(MemberId),
        new RolePermissions(permissions), new FixedTimeProvider(Now));

    static AccessGrantView Grant(AccessGrantScope scope, AccessGrantPrincipal? principal = null) =>
        new(TenantId, Uuid.CreateVersion4(), new AccessGrantTerms(
            principal ?? new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, MemberId),
            RoleId, scope, new AccessGrantSource("manual", "request-123"),
            ActorReference.ForMember(MemberId, "Organization Admin"), Now.AddDays(-1), null), null, null);

    sealed class GrantDirectory : IAccessGrantDirectory
    {
        readonly AccessGrantView[] _grants;
        readonly Uuid? _pendingRevocationId;
        public IReadOnlySet<Uuid> CheckedGrantIds { get; private set; } = new HashSet<Uuid>();
        public bool ProjectRevocationOnPendingScan { get; init; }
        public int ListCount { get; private set; }
        bool _revocationProjected;

        public GrantDirectory(params AccessGrantView[] grants) => _grants = grants;

        public GrantDirectory(Uuid pendingRevocationId, params AccessGrantView[] grants)
        {
            _grants = grants;
            _pendingRevocationId = pendingRevocationId;
        }

        public ValueTask<AccessGrantSetView> ListAsync(Uuid tenantId, CancellationToken ct = default)
        {
            ListCount++;
            var grants = _revocationProjected
                ? _grants.Select(grant => grant with
                {
                    RevokedAt = Now.AddMinutes(-1),
                    RevokedBy = ActorReference.ForMember(MemberId, "Organization Admin"),
                }).ToArray()
                : _grants;
            return ValueTask.FromResult(new AccessGrantSetView(tenantId, 1, grants));
        }

        public ValueTask<AccessGrantView?> GetAsync(Uuid tenantId, Uuid grantId,
            CancellationToken ct = default) => ValueTask.FromResult<AccessGrantView?>(
            _grants.SingleOrDefault(item => item.GrantId == grantId));

        public ValueTask<IReadOnlySet<Uuid>> FindPendingRevocationsAsync(Uuid tenantId,
            IReadOnlySet<Uuid> grantIds,
            CancellationToken ct = default)
        {
            CheckedGrantIds = grantIds;
            if (ProjectRevocationOnPendingScan)
                _revocationProjected = true;
            HashSet<Uuid> pending = _pendingRevocationId is { } id && grantIds.Contains(id)
                ? [id]
                : [];
            return ValueTask.FromResult<IReadOnlySet<Uuid>>(pending);
        }
    }

    sealed class MembershipDirectory(string affiliation) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult<TenantMembershipView?>(
            new TenantMembershipView(userId, Uuid.Parse(tenantId, CultureInfo.InvariantCulture), affiliation));

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(true);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class TeamMemberDirectory(params Uuid[] memberIds) : ITeamMemberDirectoryReader
    {
        public bool PendingRemoval { get; init; }
        public bool ProjectRemovalOnPendingScan { get; init; }
        public int ListCount { get; private set; }
        bool _removalProjected;

        public ValueTask<Page<TeamMemberView>> ListAsync(Uuid tenantId, Uuid teamId, int? limit,
            string? cursor, string? search, bool descending, CancellationToken ct = default)
        {
            ListCount++;
            return ValueTask.FromResult(new Page<TeamMemberView>(_removalProjected
                ? []
                : memberIds.Select(memberId => new TeamMemberView(teamId, memberId)).ToArray(), null));
        }

        public ValueTask<bool> HasPendingRemovalAsync(Uuid tenantId, Uuid teamId, Uuid memberId,
            CancellationToken ct = default)
        {
            if (ProjectRemovalOnPendingScan)
                _removalProjected = true;
            return ValueTask.FromResult(PendingRemoval);
        }
    }

    sealed class RolePermissions(IReadOnlyList<string> permissions) : IRolePermissionDirectoryReader
    {
        public ValueTask<Page<RolePermissionView>> ListAsync(Uuid tenantId, Uuid roleId, int? limit,
            string? cursor, string? search, bool descending, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<RolePermissionView>(permissions.Select(permission =>
                new RolePermissionView(roleId, permission)).ToArray(), null));
    }

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
