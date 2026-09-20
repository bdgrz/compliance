using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetMemberAccessHandler(ITenantMembershipDirectoryReader memberships,
    IMemberAccessReader access, ITeamDirectoryReader teams, IRoleDirectoryReader roles)
    : IRequestHandler<GetMemberAccess, MemberAccessView>
{
    public async ValueTask<Result<MemberAccessView>> HandleAsync(
        IRequestContext<GetMemberAccess> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.ExpectedBuiltInRole is { } expectedRole &&
            BuiltInRbac.RoleIdForRole(request.TenantId, expectedRole) is null)
            return Result<MemberAccessView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The expected built-in role is not supported."));
        if (!await memberships.IsMemberAsync(request.TenantId.ToString(), request.UserId, ct)
                .ConfigureAwait(false))
            return Result<MemberAccessView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The member was not found."));

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
        var permissions = paths.SelectMany(path => path.Permissions)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return Result<MemberAccessView>.Success(new MemberAccessView(request.TenantId,
            request.UserId, memberId, paths, permissions));
    }
}
