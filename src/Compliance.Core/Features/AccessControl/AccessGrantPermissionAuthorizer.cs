using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public interface IAccessGrantPermissionAuthorizer
{
    ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, Uuid programId,
        string permission, CancellationToken ct = default);

    ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId, Uuid userId,
        Uuid memberId, string permission, CancellationToken ct = default);
}

public interface IAccessGrantScopePermissionAuthorizer
{
    ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId, Uuid memberId,
        IReadOnlyCollection<AccessGrantScope> scopes, string permission,
        CancellationToken ct = default);

    ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId, Uuid userId,
        Uuid memberId, string permission, CancellationToken ct = default);
}

/// <summary>Evaluates one program operation against current membership and active scoped grants.</summary>
sealed class AccessGrantPermissionAuthorizer(IAccessGrantDirectory grants,
    ITenantMembershipDirectoryReader memberships, ITeamMemberDirectoryReader teamMembers,
    IRolePermissionDirectoryReader rolePermissions, IAggregateReader reader, TimeProvider clock,
    IMemberAccessEligibility sourceMember)
    : IAccessGrantPermissionAuthorizer, IAccessGrantScopePermissionAuthorizer
{
    const int PageSize = 200;

    public async ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
        Uuid programId, string permission, CancellationToken ct = default)
    {
        if (programId == Uuid.Empty)
            return false;
        var visibility = await GetProgramVisibilityAsync(tenantId, userId, memberId,
            permission, ct).ConfigureAwait(false);
        return visibility.OrganizationWide || visibility.ProgramIds.Contains(programId);
    }

    public async ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId,
        Uuid userId, Uuid memberId, string permission, CancellationToken ct = default)
    {
        var eligibleGrants = await GetEligibleGrantsAsync(tenantId, userId, memberId,
            permission, scope => CoversAnyProgram(scope, tenantId), ct).ConfigureAwait(false);
        var organizationWide = eligibleGrants.Any(grant =>
            grant.Scope.Kind == AccessGrantScopeKind.Organization && grant.Scope.Id == tenantId);
        var programIds = eligibleGrants.Where(grant =>
                grant.Scope.Kind == AccessGrantScopeKind.Program)
            .Select(grant => grant.Scope.Id).ToHashSet();
        return new ProgramAccessVisibility(organizationWide, programIds);
    }

    public async ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId,
        Uuid memberId, IReadOnlyCollection<AccessGrantScope> scopes, string permission,
        CancellationToken ct = default)
    {
        if (scopes is null || scopes.Count == 0)
            return false;
        var requested = scopes.Where(scope => scope is not null && scope.Id != Uuid.Empty &&
                Enum.IsDefined(scope.Kind) &&
                (scope.Kind != AccessGrantScopeKind.Organization || scope.Id == tenantId))
            .ToHashSet();
        if (requested.Count == 0)
            return false;
        var eligible = await GetEligibleGrantsAsync(tenantId, userId, memberId, permission,
            requested.Contains, ct).ConfigureAwait(false);
        return eligible.Count != 0;
    }

    public async ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId,
        Uuid userId, Uuid memberId, string permission, CancellationToken ct = default)
    {
        var eligible = await GetEligibleGrantsAsync(tenantId, userId, memberId, permission,
            scope => scope.Id != Uuid.Empty &&
                (scope.Kind == AccessGrantScopeKind.Organization && scope.Id == tenantId ||
                 scope.Kind is AccessGrantScopeKind.Application or
                     AccessGrantScopeKind.SystemInstance), ct).ConfigureAwait(false);
        return eligible.Count != 0;
    }

    async ValueTask<IReadOnlyList<EffectiveGrant>> GetEligibleGrantsAsync(Uuid tenantId,
        Uuid userId, Uuid memberId, string permission, Func<AccessGrantScope, bool> scopeMatches,
        CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || userId == Uuid.Empty ||
            memberId != RbacIds.Member(tenantId, userId) || string.IsNullOrWhiteSpace(permission))
            return [];

        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is not { Affiliation: "client_personnel", IsSuspended: false } ||
            membership.TenantId != tenantId || membership.UserId != userId ||
            !await sourceMember.IsEligibleAsync(tenantId, userId, ct).ConfigureAwait(false))
            return [];

        var access = await grants.ListAsync(tenantId, ct).ConfigureAwait(false);
        if (access.TenantId != tenantId)
            return [];

        var now = clock.GetUtcNow();
        var eligibleGrants = new Dictionary<Uuid, EffectiveGrant>();
        var principalMatches = new Dictionary<AccessGrantPrincipal, bool>();
        var rolePermissions = new Dictionary<Uuid, bool>();
        foreach (var grant in access.Grants)
        {
            var scope = grant.Terms.Scope;
            if (grant.TenantId != tenantId || grant.RevokedAt is not null ||
                grant.Terms.EffectiveFrom > now ||
                grant.Terms.EffectiveUntil is { } until && until <= now || !scopeMatches(scope))
                continue;

            if (!principalMatches.TryGetValue(grant.Terms.Principal, out var includesMember))
            {
                includesMember = await IncludesPrincipalAsync(tenantId, memberId,
                    grant.Terms.Principal, ct).ConfigureAwait(false);
                principalMatches[grant.Terms.Principal] = includesMember;
            }
            if (!includesMember)
                continue;

            if (!rolePermissions.TryGetValue(grant.Terms.RoleId, out var hasPermission))
            {
                hasPermission = await RoleHasPermissionAsync(tenantId, grant.Terms.RoleId,
                    permission, ct).ConfigureAwait(false);
                rolePermissions[grant.Terms.RoleId] = hasPermission;
            }
            if (hasPermission)
                eligibleGrants[grant.GrantId] = new EffectiveGrant(scope,
                    grant.Terms.Principal, grant.Terms.RoleId);
        }

        if (eligibleGrants.Count == 0)
            return [];

        var pendingRevocations = await grants.FindPendingRevocationsAsync(tenantId,
            eligibleGrants.Keys.ToHashSet(), ct).ConfigureAwait(false);
        // The projector may catch up between the first list and the event-tail scan. In that
        // case the scan starts beyond the revoke, so confirm the projected state once more.
        var currentAccess = await grants.ListAsync(tenantId, ct).ConfigureAwait(false);
        if (currentAccess.TenantId != tenantId)
            return [];
        var currentGrantIds = currentAccess.Grants
            .Where(grant => grant.TenantId == tenantId && grant.RevokedAt is null)
            .Select(grant => grant.GrantId).ToHashSet();
        var effective = new List<EffectiveGrant>();
        foreach (var (grantId, candidate) in eligibleGrants)
        {
            if (pendingRevocations.Contains(grantId) || !currentGrantIds.Contains(grantId))
                continue;
            // The team projector may also catch up while the grant tail is scanned.
            if (candidate.Principal.Kind == AccessGrantPrincipalKind.Team &&
                !await IncludesPrincipalAsync(tenantId, memberId, candidate.Principal, ct)
                    .ConfigureAwait(false))
                continue;
            // Role, permission, and team deletion facts can precede their directories and the
            // asynchronous cleanup reactors. Recheck their tenant-addressed source streams at
            // the deciding read, after the grant and team projection-lag checks above.
            if (!await HasCurrentRolePermissionAsync(tenantId, candidate.RoleId, permission, ct)
                    .ConfigureAwait(false))
                continue;
            if (candidate.Principal.Kind == AccessGrantPrincipalKind.Team)
            {
                var team = await reader.HydrateAsync(new Team(tenantId, candidate.Principal.Id), ct)
                    .ConfigureAwait(false);
                if (!team.IsActive)
                    continue;
            }
            effective.Add(candidate);
        }
        // A suspension can commit during the grant, team, or role reads. Hydrate the
        // member again at the final positive decision point.
        if (effective.Count != 0 &&
            !await sourceMember.IsEligibleAsync(tenantId, userId, ct).ConfigureAwait(false))
            return [];
        return effective;
    }

    async ValueTask<bool> IncludesPrincipalAsync(Uuid tenantId, Uuid memberId,
        AccessGrantPrincipal principal, CancellationToken ct)
    {
        if (principal.Kind == AccessGrantPrincipalKind.Member)
            return principal.Id == memberId;
        if (principal.Kind != AccessGrantPrincipalKind.Team || principal.Id == Uuid.Empty)
            return false;

        string? cursor = null;
        var isProjectedMember = false;
        do
        {
            var page = await teamMembers.ListAsync(tenantId, principal.Id, PageSize, cursor,
                memberId.ToString(), descending: false, ct).ConfigureAwait(false);
            if (page.Items.Any(item => item.TeamId == principal.Id && item.MemberId == memberId))
            {
                isProjectedMember = true;
                break;
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        if (!isProjectedMember || await teamMembers.HasPendingRemovalAsync(tenantId,
                principal.Id, memberId, ct).ConfigureAwait(false))
            return false;

        // A removal can project after the first list and before the tail scan's checkpoint.
        cursor = null;
        do
        {
            var page = await teamMembers.ListAsync(tenantId, principal.Id, PageSize, cursor,
                memberId.ToString(), descending: false, ct).ConfigureAwait(false);
            if (page.Items.Any(item => item.TeamId == principal.Id && item.MemberId == memberId))
                return true;
            cursor = page.NextCursor;
        } while (cursor is not null);
        return false;
    }

    async ValueTask<bool> RoleHasPermissionAsync(Uuid tenantId, Uuid roleId, string permission,
        CancellationToken ct)
    {
        if (roleId == Uuid.Empty)
            return false;

        string? cursor = null;
        do
        {
            var page = await rolePermissions.ListAsync(tenantId, roleId, PageSize, cursor,
                search: null, descending: false, ct).ConfigureAwait(false);
            if (page.Items.Any(item => item.RoleId == roleId &&
                    string.Equals(item.Permission, permission, StringComparison.Ordinal)))
                return true;
            cursor = page.NextCursor;
        } while (cursor is not null);

        return false;
    }

    async ValueTask<bool> HasCurrentRolePermissionAsync(Uuid tenantId, Uuid roleId,
        string permission, CancellationToken ct)
    {
        var rolePermission = await reader.HydrateAsync(new RolePermission(tenantId, roleId,
                permission), ct).ConfigureAwait(false);
        if (!rolePermission.IsAssigned)
            return false;
        var role = await reader.HydrateAsync(new Role(tenantId, roleId), ct)
            .ConfigureAwait(false);
        return role.IsActive;
    }

    static bool CoversAnyProgram(AccessGrantScope scope, Uuid tenantId) =>
        scope.Kind switch
        {
            AccessGrantScopeKind.Organization => scope.Id == tenantId,
            AccessGrantScopeKind.Program => scope.Id != Uuid.Empty,
            _ => false,
        };

    sealed record EffectiveGrant(AccessGrantScope Scope, AccessGrantPrincipal Principal,
        Uuid RoleId);
}
