using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

sealed class ProgramManagementAuthorizer(ITenantMembershipDirectoryReader memberships,
    ITenantActivity tenants,
    IAccessGrantPermissionAuthorizer scopedPermissions,
    IProgramResourceScopeResolver resourceScopes)
    : IRequestAuthorizer<IProgramManagementRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IProgramManagementRequest> context,
        CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Program administration requires a Bdgrz user identity."));
        var tenantId = context.Request.TenantId;
        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct).ConfigureAwait(false);
        if (membership is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant was not found."));
        if (membership.Affiliation == "firm_staff")
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Firm staff require an accepted engagement to access client work."));
        if (!await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The tenant is not active."));
        var memberId = RbacIds.Member(tenantId, userId);

        if (context.Request is IProgramScopedRequest scoped)
        {
            if (!await resourceScopes.IsTenantProgramAsync(tenantId, scoped.ProgramId, ct)
                    .ConfigureAwait(false))
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The program was not found."));
            var scopedAllowed = await scopedPermissions.IsAllowedAsync(tenantId, userId, memberId,
                    scoped.ProgramId, scoped.RequiredPermission, ct)
                .ConfigureAwait(false);
            return scopedAllowed
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "The actor may not manage this program."));
        }

        if (context.Request is PreviewApplicationChange preview)
        {
            // The preview summarizes references across an application and may cross Programs.
            // A grant to one Program cannot authorize the whole tenant-owned response.
            if (!await resourceScopes.IsTenantApplicationAsync(tenantId, preview.ApplicationId, ct)
                    .ConfigureAwait(false))
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The application was not found."));
            var visibility = await scopedPermissions.GetProgramVisibilityAsync(tenantId, userId,
                    memberId, IProgramReadRequest.ReadPermission, ct)
                .ConfigureAwait(false);
            return visibility.OrganizationWide
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "Application impact preview requires organization-wide program access."));
        }

        if (context.Request is ListPrograms or ListClientServices)
        {
            var visibility = await scopedPermissions.GetProgramVisibilityAsync(tenantId, userId,
                    memberId, IProgramReadRequest.ReadPermission, ct)
                .ConfigureAwait(false);
            return visibility.HasAnyAccess
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "The actor may not access any program in this organization."));
        }

        if (context.Request is IProgramResourceRequest resource)
        {
            var programId = await resourceScopes.ResolveProgramIdAsync(tenantId, resource, ct)
                .ConfigureAwait(false);
            if (programId is null)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The program-owned resource was not found."));
            var allowed = await scopedPermissions.IsAllowedAsync(tenantId, userId, memberId,
                    programId.Value, resource.RequiredPermission, ct)
                .ConfigureAwait(false);
            return allowed
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "The actor may not access this program-owned resource."));
        }

        // Program creation is organization-scoped; a grant to one existing program cannot
        // authorize creating another. Existing program records must carry or resolve that scope.
        if (context.Request is CreateProgram)
        {
            var organizationAccess = await scopedPermissions.GetProgramVisibilityAsync(tenantId,
                    userId, memberId, IProgramScopedRequest.ManagementPermission, ct)
                .ConfigureAwait(false);
            return organizationAccess.OrganizationWide
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "Program creation requires an organization-wide grant."));
        }

        return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "The request does not identify an authorized program scope."));
    }
}
