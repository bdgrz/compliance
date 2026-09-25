using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

static class ClientServiceActor
{
    public static (Uuid MemberId, string Display) Snapshot<T>(IRequestContext<T> context)
        where T : IRequestBase
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var tenantId = context.Request switch
        {
            IProgramManagementRequest request => request.TenantId,
            _ => throw new InvalidOperationException("A service command requires tenant scope."),
        };
        return (RbacIds.Member(tenantId, userId),
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
    }
}
