using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

static class TechnologyInventoryActor
{
    public static ActorReference From<T>(IRequestContext<T> context) where T : IRequestBase
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException(
                "TechnologyInventoryAuthorizer must reject this actor.");
        var tenantId = ((ITechnologyInventoryRequest)context.Request).TenantId;
        return ActorReference.ForMember(RbacIds.Member(tenantId, userId),
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
    }
}
