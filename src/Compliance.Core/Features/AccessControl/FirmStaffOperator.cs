using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

static class FirmStaffOperator
{
    internal static ActorReference From<T>(IRequestContext<T> context) where T : IRequestBase
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject : throw new InvalidOperationException("FirmStaffAdministrationAuthorizer must reject this actor.");
        return ActorReference.ForPlatformOperator(userId, UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
    }
}
