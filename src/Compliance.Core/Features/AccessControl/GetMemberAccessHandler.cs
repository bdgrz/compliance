using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetMemberAccessHandler(ITenantMembershipDirectoryReader memberships,
    IMemberAccessReader access, ITeamDirectoryReader teams, IRoleDirectoryReader roles,
    IAccessGrantDirectory grants, ITeamMemberDirectoryReader teamMembers,
    IRolePermissionDirectoryReader rolePermissions, IAggregateReader reader, TimeProvider clock,
    IMemberAccessEligibility sourceMember)
    : IRequestHandler<GetMemberAccess, MemberAccessView>
{
    const int PageSize = 200;

    public async ValueTask<Result<MemberAccessView>> HandleAsync(
        IRequestContext<GetMemberAccess> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.ExpectedBuiltInRole is { } expectedRole &&
            BuiltInRbac.RoleIdForRole(request.TenantId, expectedRole) is null)
            return Result<MemberAccessView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The expected built-in role is not supported."));
        var membership = await memberships.GetAsync(request.TenantId.ToString(), request.UserId, ct)
            .ConfigureAwait(false);
        if (membership is null || membership.TenantId != request.TenantId ||
            membership.UserId != request.UserId)
            return Result<MemberAccessView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The member was not found."));
        var memberIsActive = !membership.IsSuspended &&
            membership.Affiliation == "client_personnel" &&
            await sourceMember.IsEligibleAsync(request.TenantId, request.UserId, ct)
                .ConfigureAwait(false);

        var memberId = RbacIds.Member(request.TenantId, request.UserId);
        var edges = await access.ReadAsync(request.TenantId, memberId, ct).ConfigureAwait(false);
        if (request.ExpectedBuiltInRole is { } role &&
            BuiltInRbac.RoleIdForRole(request.TenantId, role) is { } roleId &&
            !edges.Any(edge => edge.RoleId == roleId))
            return Result<MemberAccessView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The expected role has not reached the access projection."));
        var paths = new List<MemberAccessPath>(edges.Count);
        foreach (var edge in edges)
        {
            var team = await teams.GetAsync(request.TenantId, edge.TeamId, ct).ConfigureAwait(false);
            var roleView = await roles.GetAsync(request.TenantId, edge.RoleId, ct).ConfigureAwait(false);
            if (team is null || roleView is null)
                return Result<MemberAccessView>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The access explanation projection is still catching up."));
            paths.Add(new MemberAccessPath(edge.TeamId, team.Name, edge.RoleId, roleView.Name,
                edge.Permissions));
        }
        var grantSet = await grants.ListAsync(request.TenantId, ct).ConfigureAwait(false);
        if (grantSet.TenantId != request.TenantId)
            return Result<MemberAccessView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The access grant projection is still catching up."));

        var teamGrantIds = grantSet.Grants
            .Where(grant => grant.TenantId == request.TenantId &&
                grant.Terms.Principal.Kind == AccessGrantPrincipalKind.Team)
            .Select(grant => grant.Terms.Principal.Id).Distinct().ToArray();
        var memberTeams = await FindCurrentTeamsAsync(request.TenantId, memberId, teamGrantIds, ct)
            .ConfigureAwait(false);
        var memberGrants = grantSet.Grants.Where(grant => grant.TenantId == request.TenantId &&
            (grant.Terms.Principal.Kind == AccessGrantPrincipalKind.Member &&
                grant.Terms.Principal.Id == memberId ||
             grant.Terms.Principal.Kind == AccessGrantPrincipalKind.Team &&
                memberTeams.Contains(grant.Terms.Principal.Id))).ToArray();
        var pendingRevocations = await grants.FindPendingRevocationsAsync(request.TenantId,
            memberGrants.Select(grant => grant.GrantId).ToHashSet(), ct).ConfigureAwait(false);
        // A revocation can project between the first grant list and the event-tail scan.
        var currentGrantSet = memberGrants.Length == 0
            ? grantSet
            : await grants.ListAsync(request.TenantId, ct).ConfigureAwait(false);
        if (currentGrantSet.TenantId != request.TenantId)
            return Result<MemberAccessView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The access grant projection is still catching up."));
        // Team removal may project while the grant event tail is scanned. Use this final
        // membership state to decide which team paths still belong to the member.
        var currentMemberTeams = await FindCurrentTeamsAsync(request.TenantId, memberId,
            teamGrantIds, ct).ConfigureAwait(false);
        memberGrants = memberGrants.Where(grant =>
            grant.Terms.Principal.Kind != AccessGrantPrincipalKind.Team ||
            currentMemberTeams.Contains(grant.Terms.Principal.Id)).ToArray();
        var now = clock.GetUtcNow();
        var grantPaths = new List<MemberAccessGrantPath>(memberGrants.Length);
        var organizationGrantPermissions = new List<string>();
        foreach (var originalGrant in memberGrants)
        {
            var currentGrant = currentGrantSet.Grants.SingleOrDefault(item =>
                item.TenantId == request.TenantId && item.GrantId == originalGrant.GrantId);
            var grant = currentGrant ?? originalGrant;
            var roleView = await roles.GetAsync(request.TenantId, grant.Terms.RoleId, ct)
                .ConfigureAwait(false);
            var sourceRole = await reader.HydrateAsync(new Role(request.TenantId,
                    grant.Terms.RoleId), ct).ConfigureAwait(false);
            var permissionSet = sourceRole.IsActive
                ? await ReadCurrentRolePermissionsAsync(request.TenantId,
                    grant.Terms.RoleId, ct).ConfigureAwait(false)
                : [];
            var teamIsActive = true;
            if (grant.Terms.Principal.Kind == AccessGrantPrincipalKind.Team)
            {
                var sourceTeam = await reader.HydrateAsync(new Team(request.TenantId,
                        grant.Terms.Principal.Id), ct).ConfigureAwait(false);
                teamIsActive = sourceTeam.IsActive;
            }
            var scope = grant.Terms.Scope;
            var scopeIsEffective = scope.Kind switch
            {
                AccessGrantScopeKind.Organization => scope.Id == request.TenantId,
                AccessGrantScopeKind.Program => scope.Id != Uuid.Empty,
                _ => false,
            };
            var isEffective = memberIsActive &&
                currentGrant is not null && currentGrant.Terms == originalGrant.Terms &&
                grant.RevokedAt is null && !pendingRevocations.Contains(grant.GrantId) &&
                grant.Terms.EffectiveFrom <= now &&
                (grant.Terms.EffectiveUntil is null || grant.Terms.EffectiveUntil > now) &&
                scopeIsEffective && sourceRole.IsActive && teamIsActive && permissionSet.Count > 0;
            grantPaths.Add(new MemberAccessGrantPath(grant,
                roleView?.Name ?? grant.Terms.RoleId.ToString(), permissionSet, isEffective));
            if (isEffective && scope.Kind == AccessGrantScopeKind.Organization &&
                scope.Id == request.TenantId)
                organizationGrantPermissions.AddRange(permissionSet);
        }

        // Paths retain the projected assignment history for review. Compute effective standing
        // permissions from source relationships so removals outrun their projection safely.
        var standingPermissions = memberIsActive
            ? await ReadCurrentStandingPermissionsAsync(request.TenantId, memberId, edges, ct)
                .ConfigureAwait(false)
            : [];
        // A suspension can commit while source relationships are read. Keep the historical
        // explanation but clear every effective path at the final source decision point.
        var currentlyActive = memberIsActive &&
            await sourceMember.IsEligibleAsync(request.TenantId, request.UserId, ct)
                .ConfigureAwait(false);
        if (!currentlyActive)
            grantPaths = grantPaths.Select(path => path with { IsEffective = false }).ToList();
        var permissions = currentlyActive
            ? standingPermissions.Concat(organizationGrantPermissions)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            : [];
        return Result<MemberAccessView>.Success(new MemberAccessView(request.TenantId,
            request.UserId, memberId, paths, grantPaths, permissions));
    }

    async ValueTask<IReadOnlyList<string>> ReadCurrentStandingPermissionsAsync(Uuid tenantId,
        Uuid memberId, IReadOnlyList<MemberAccessEdge> edges, CancellationToken ct)
    {
        var effective = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            var team = await reader.HydrateAsync(new Team(tenantId, edge.TeamId), ct)
                .ConfigureAwait(false);
            if (!team.IsActive)
                continue;
            var member = await reader.HydrateAsync(new TeamMember(tenantId, edge.TeamId,
                    memberId), ct).ConfigureAwait(false);
            if (!member.IsAssigned)
                continue;
            var teamRole = await reader.HydrateAsync(new TeamRole(tenantId, edge.TeamId,
                    edge.RoleId), ct).ConfigureAwait(false);
            if (!teamRole.IsAssigned)
                continue;
            var role = await reader.HydrateAsync(new Role(tenantId, edge.RoleId), ct)
                .ConfigureAwait(false);
            if (!role.IsActive)
                continue;
            foreach (var permission in edge.Permissions)
            {
                var rolePermission = await reader.HydrateAsync(new RolePermission(tenantId,
                        edge.RoleId, permission), ct).ConfigureAwait(false);
                if (rolePermission.IsAssigned)
                    effective.Add(permission);
            }
        }
        return effective.ToArray();
    }

    async ValueTask<HashSet<Uuid>> FindCurrentTeamsAsync(Uuid tenantId, Uuid memberId,
        IReadOnlyCollection<Uuid> candidateTeamIds, CancellationToken ct)
    {
        var memberTeams = new HashSet<Uuid>();
        foreach (var teamId in candidateTeamIds)
        {
            if (await HasProjectedTeamMemberAsync(tenantId, teamId, memberId, ct)
                    .ConfigureAwait(false) &&
                !await teamMembers.HasPendingRemovalAsync(tenantId, teamId, memberId, ct)
                    .ConfigureAwait(false) &&
                // The member removal can project before the tail scan begins.
                await HasProjectedTeamMemberAsync(tenantId, teamId, memberId, ct)
                    .ConfigureAwait(false))
                memberTeams.Add(teamId);
        }
        return memberTeams;
    }

    async ValueTask<bool> HasProjectedTeamMemberAsync(Uuid tenantId, Uuid teamId,
        Uuid memberId, CancellationToken ct)
    {
        string? cursor = null;
        do
        {
            var page = await teamMembers.ListAsync(tenantId, teamId, PageSize, cursor,
                memberId.ToString(), descending: false, ct).ConfigureAwait(false);
            if (page.Items.Any(item => item.TeamId == teamId && item.MemberId == memberId))
                return true;
            cursor = page.NextCursor;
        } while (cursor is not null);
        return false;
    }

    async ValueTask<IReadOnlyList<string>> ReadCurrentRolePermissionsAsync(Uuid tenantId, Uuid roleId,
        CancellationToken ct)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            var page = await rolePermissions.ListAsync(tenantId, roleId, PageSize, cursor,
                search: null, descending: false, ct).ConfigureAwait(false);
            foreach (var item in page.Items.Where(item => item.RoleId == roleId))
                permissions.Add(item.Permission);
            cursor = page.NextCursor;
        } while (cursor is not null);

        var assigned = new List<string>(permissions.Count);
        foreach (var permission in permissions.Order(StringComparer.Ordinal))
        {
            var source = await reader.HydrateAsync(new RolePermission(tenantId, roleId,
                    permission), ct).ConfigureAwait(false);
            if (source.IsAssigned)
                assigned.Add(permission);
        }
        return assigned;
    }
}
