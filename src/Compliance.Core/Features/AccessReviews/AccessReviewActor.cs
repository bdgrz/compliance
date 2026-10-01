using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>The acting member of an authorized access-review request.</summary>
readonly record struct AccessReviewActor(Uuid UserId, Uuid MemberId, ActorReference Reference)
{
    public static AccessReviewActor From<T>(IRequestContext<T> context) where T : IRequestBase
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("AccessReviewAuthorizer must reject this actor.");
        var tenantId = ((IAccessReviewRequest)context.Request).TenantId;
        var memberId = RbacIds.Member(tenantId, userId);
        return new AccessReviewActor(userId, memberId,
            ActorReference.ForMember(memberId, UserIdentityClaims.BdgrzDisplay(context.Actor, userId)));
    }
}
