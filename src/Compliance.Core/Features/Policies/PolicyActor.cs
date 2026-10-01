using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>The acting member resolved from an authorized request.</summary>
public sealed record PolicyActor(Uuid UserId, Uuid MemberId, ActorReference Reference)
{
    public static PolicyActor From<TRequest>(IRequestContext<TRequest> context, Uuid tenantId)
        where TRequest : IRequestBase
    {
        ArgumentNullException.ThrowIfNull(context);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var memberId = RbacIds.Member(tenantId, userId);
        return new PolicyActor(userId, memberId, ActorReference.ForMember(memberId,
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId)));
    }
}
