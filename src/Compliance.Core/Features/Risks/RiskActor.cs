using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>The acting tenant member for risk writes, already admitted by program authorization.</summary>
readonly record struct RiskActor(Uuid UserId, Uuid MemberId, string Display)
{
    public static RiskActor From(ClaimsPrincipal actor, Uuid tenantId)
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return new RiskActor(userId, RbacIds.Member(tenantId, userId),
            UserIdentityClaims.BdgrzDisplay(actor, userId));
    }
}
