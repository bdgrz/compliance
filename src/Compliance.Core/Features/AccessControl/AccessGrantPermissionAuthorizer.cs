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

/// <summary>Evaluates one program operation against current membership and active scoped grants.</summary>
sealed class AccessGrantPermissionAuthorizer(IAccessGrantDirectory grants,
    ITenantMembershipDirectoryReader memberships, ITeamMemberDirectoryReader teamMembers,
    IRolePermissionDirectoryReader rolePermissions, IAggregateReader reader, TimeProvider clock)
    : IAccessGrantPermissionAuthorizer
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
        if (tenantId == Uuid.Empty || userId == Uuid.Empty ||
            memberId != RbacIds.Member(tenantId, userId) || string.IsNullOrWhiteSpace(permission))
            return new ProgramAccessVisibility(false, new HashSet<Uuid>());

        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is not { Affiliation: "client_personnel" } ||
            membership.TenantId != tenantId || membership.UserId != userId)
            return new ProgramAccessVisibility(false, new HashSet<Uuid>());

        var access = await grants.ListAsync(tenantId, ct).ConfigureAwait(false);
        if (access.TenantId != tenantId)
            return new ProgramAccessVisibility(false, new HashSet<Uuid>());

        var now = clock.GetUtcNow();
        var eligibleGrants = new Dictionary<Uuid,
            (AccessGrantScope Scope, AccessGrantPrincipal Principal, Uuid RoleId)>();
        var principalMatches = new Dictionary<AccessGrantPrincipal, bool>();
        var rolePermissions = new Dictionary<Uuid, bool>();
        foreach (var grant in access.Grants)
        {
            var scope = grant.Terms.Scope;
            if (grant.TenantId != tenantId || grant.RevokedAt is not null ||
                grant.Terms.EffectiveFrom > now ||
                grant.Terms.EffectiveUntil is { } until && until <= now ||
                !CoversAnyProgram(scope, tenantId))
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
                eligibleGrants[grant.GrantId] = (scope, grant.Terms.Principal,
                    grant.Terms.RoleId);
        }

        if (eligibleGrants.Count == 0)
            return new ProgramAccessVisibility(false, new HashSet<Uuid>());

        var pendingRevocations = await grants.FindPendingRevocationsAsync(tenantId,
            eligibleGrants.Keys.ToHashSet(), ct).ConfigureAwait(false);
        // The projector may catch up between the first list and the event-tail scan. In that
        // case the scan starts beyond the revoke, so confirm the projected state once more.
        var currentAccess = await grants.ListAsync(tenantId, ct).ConfigureAwait(false);
        if (currentAccess.TenantId != tenantId)
            return new ProgramAccessVisibility(false, new HashSet<Uuid>());
        var currentGrantIds = currentAccess.Grants
            .Where(grant => grant.TenantId == tenantId && grant.RevokedAt is null)
            .Select(grant => grant.GrantId).ToHashSet();
        var organizationWide = false;
        var programIds = new HashSet<Uuid>();
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
            var scope = candidate.Scope;
            if (scope.Kind == AccessGrantScopeKind.Organization)
                organizationWide = true;
            else if (scope.Kind == AccessGrantScopeKind.Program)
                programIds.Add(scope.Id);
        }
        return new ProgramAccessVisibility(organizationWide, programIds);
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
}
