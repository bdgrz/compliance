using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

static class ProviderActor
{
    public static ActorReference From<T>(IRequestContext<T> context) where T : IProviderRequest
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject) ? subject :
            throw new InvalidOperationException("ProviderAuthorizer must reject this actor.");
        return ActorReference.ForMember(RbacIds.Member(context.Request.TenantId, userId),
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
    }
}
