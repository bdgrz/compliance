using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationActor
{
    public static (Uuid MemberId, string Display) From<T>(IRequestContext<T> context)
        where T : IRequestBase
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ApplicationInventoryAuthorizer must reject this actor.");
        var tenantId = ((IApplicationInventoryRequest)context.Request).TenantId;
        return (RbacIds.Member(tenantId, userId),
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
    }
}
