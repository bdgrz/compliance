using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>The acting tenant member behind a readiness command the program authorizer admitted.</summary>
readonly record struct ReadinessActor(Uuid MemberId, string Display)
{
    public static ReadinessActor From(IRequestContext context, Uuid tenantId)
    {
        ArgumentNullException.ThrowIfNull(context);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return new ReadinessActor(RbacIds.Member(tenantId, userId),
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
    }
}
