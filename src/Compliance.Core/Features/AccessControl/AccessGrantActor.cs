using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

static class AccessGrantActor
{
    public static ActorReference From<TRequest>(IRequestContext<TRequest> context, Uuid tenantId)
        where TRequest : IRequestBase
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subjectId)
            ? subjectId
            : throw new InvalidOperationException("RbacManagementAuthorizer must reject this actor.");
        return ActorReference.ForMember(RbacIds.Member(tenantId, userId),
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
    }
}
