using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Applies explicit restricted-read permissions to application inventory views.</summary>
public sealed class RestrictedApplicationVisibility(IPermissionAuthorizer permissions,
    IAccessGrantScopePermissionAuthorizer scopedPermissions, IAggregateReader reader,
    IApplicationDirectoryReader? applications = null)
{
    public async ValueTask<bool> CanReadApplicationAsync(Uuid tenantId, Uuid userId,
        Uuid applicationId, CancellationToken ct = default)
    {
        var application = await CurrentApplicationAsync(tenantId, applicationId, ct)
            .ConfigureAwait(false);
        if (!application.IsCreated)
            return false;
        if (!application.IsRestricted)
            return true;
        if (await HasOrganizationPermissionAsync(tenantId, userId, ct).ConfigureAwait(false))
            return true;
        return await scopedPermissions.IsAllowedAtAnyScopeAsync(tenantId, userId,
            RbacIds.Member(tenantId, userId), ApplicationScopes(tenantId, applicationId),
            RbacPermissions.ApplicationRestrictedRead, ct).ConfigureAwait(false);
    }

    public async ValueTask<bool> CanReadSystemInstanceAsync(Uuid tenantId, Uuid userId,
        Uuid applicationId, Uuid systemInstanceId, CancellationToken ct = default)
    {
        var application = await CurrentApplicationAsync(tenantId, applicationId, ct)
            .ConfigureAwait(false);
        if (!application.IsCreated)
            return false;
        if (!application.IsRestricted)
            return true;
        if (await HasOrganizationPermissionAsync(tenantId, userId, ct).ConfigureAwait(false))
            return true;
        var scopes = ApplicationScopes(tenantId, applicationId)
            .Append(new AccessGrantScope(AccessGrantScopeKind.SystemInstance, systemInstanceId))
            .ToArray();
        return await scopedPermissions.IsAllowedAtAnyScopeAsync(tenantId, userId,
            RbacIds.Member(tenantId, userId), scopes,
            RbacPermissions.ApplicationRestrictedRead, ct).ConfigureAwait(false);
    }

    public async ValueTask<bool> CanReadSystemInstanceAsync(Uuid tenantId, Uuid userId,
        Uuid systemInstanceId, CancellationToken ct = default)
    {
        if (systemInstanceId == Uuid.Empty)
            return false;
        var instance = await reader.HydrateAsync(new DeclaredSystemInstance(tenantId,
            systemInstanceId), ct).ConfigureAwait(false);
        var applicationId = instance.IsCreated ? instance.ApplicationId : Uuid.Empty;
        if (applicationId == Uuid.Empty && applications is not null)
        {
            var projected = await applications.GetInstanceAsync(tenantId, systemInstanceId, ct)
                .ConfigureAwait(false);
            if (projected is null || projected.TenantId != tenantId ||
                projected.SystemInstanceId != systemInstanceId)
                return false;
            applicationId = projected.ApplicationId;
        }
        return applicationId != Uuid.Empty && await CanReadSystemInstanceAsync(tenantId,
            userId, applicationId, systemInstanceId, ct).ConfigureAwait(false);
    }

    async ValueTask<bool> HasOrganizationPermissionAsync(Uuid tenantId, Uuid userId,
        CancellationToken ct) => await permissions.IsAllowedAsync(tenantId, userId,
            RbacIds.Member(tenantId, userId), RbacPermissions.ApplicationRestrictedRead, ct)
        .ConfigureAwait(false);

    static AccessGrantScope[] ApplicationScopes(Uuid tenantId, Uuid applicationId) =>
    [new AccessGrantScope(AccessGrantScopeKind.Organization, tenantId),
        new AccessGrantScope(AccessGrantScopeKind.Application, applicationId)];

    async ValueTask<DeclaredApplication> CurrentApplicationAsync(Uuid tenantId,
        Uuid applicationId, CancellationToken ct) =>
        await reader.HydrateApplicationAsync(tenantId, applicationId, ct)
            .ConfigureAwait(false);
}
