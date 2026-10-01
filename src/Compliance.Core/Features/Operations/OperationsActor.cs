using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>The acting tenant member for operations writes, already admitted by program authorization.</summary>
readonly record struct OperationsActor(Uuid UserId, Uuid MemberId, string Display)
{
    public static OperationsActor From(ClaimsPrincipal actor, Uuid tenantId)
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return new OperationsActor(userId, RbacIds.Member(tenantId, userId),
            UserIdentityClaims.BdgrzDisplay(actor, userId));
    }
}
