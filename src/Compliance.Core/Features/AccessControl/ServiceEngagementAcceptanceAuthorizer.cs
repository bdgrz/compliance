using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Requires a personal, current firm identity; client administration grants do not authorize acceptance.</summary>
sealed class ServiceEngagementAcceptanceAuthorizer(IAggregateReader reader,
    IPlatformUserDirectoryReader platformUsers, ITenantActivity tenants)
    : IRequestAuthorizer<IServiceEngagementAcceptanceRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IServiceEngagementAcceptanceRequest> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor))
            return Deny(RequestErrorKind.Forbidden,
                "Professional acceptance requires a personal HTTP request.");

        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Deny(RequestErrorKind.Unauthorized,
                "Professional acceptance requires a canonical Bdgrz user identity.");

        var tenantId = context.Request.TenantId;
        if (tenantId == Uuid.Empty || !await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Deny(RequestErrorKind.Forbidden, "The client tenant is not active.");

        if (!await platformUsers.ExistsAsync(userId, ct).ConfigureAwait(false))
            return Deny(RequestErrorKind.Forbidden,
                "Professional acceptance requires a current platform user identity.");

        var directory = await reader.HydrateAsync(new FirmStaffDirectory(), ct).ConfigureAwait(false);
        var identities = directory.View().Staff.Where(staff => staff.UserId == userId).ToArray();
        return identities.Length == 1 && identities[0].IsActive
            ? Result.Success
            : Deny(RequestErrorKind.Forbidden,
                "Professional acceptance requires one current active firm-staff identity.");
    }

    static Result Deny(RequestErrorKind kind, string message) =>
        Result.Failure(new RequestError(kind, message));
}
