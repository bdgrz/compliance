using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

public static class RestrictedApplicationVisibilityFixture
{
    public static RestrictedApplicationVisibility Create(IAggregateReader reader,
        IPermissionAuthorizer permissions, IApplicationDirectoryReader? applications = null) =>
        new(permissions, new FixedScopedGrantAuthorizer([]), reader, applications);

    public static RestrictedApplicationVisibility Create(IAggregateReader reader,
        bool organizationPermission = false, params AccessGrantScope[] allowedScopes) =>
        new(new FixedPermissionAuthorizer(organizationPermission),
            new FixedScopedGrantAuthorizer(allowedScopes), reader);

    sealed class FixedPermissionAuthorizer(bool allowed) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) => ValueTask.FromResult(allowed);
    }

    sealed class FixedScopedGrantAuthorizer(AccessGrantScope[] allowedScopes)
        : IAccessGrantScopePermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, IReadOnlyCollection<AccessGrantScope> scopes, string permission,
            CancellationToken ct = default) =>
            ValueTask.FromResult(scopes.Any(allowedScopes.Contains));

        public ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(allowedScopes.Any(scope =>
                scope.Kind == AccessGrantScopeKind.Organization && scope.Id == tenantId ||
                scope.Kind is AccessGrantScopeKind.Application or
                    AccessGrantScopeKind.SystemInstance));
    }
}
