using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class InviteOrganizationMemberHandler(TenantInvitationIssuer issuer)
    : IRequestHandler<InviteOrganizationMember>
{
    public ValueTask<Result> HandleAsync(IRequestContext<InviteOrganizationMember> context,
        CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return ValueTask.FromResult(Result.Failure(new RequestError(
                RequestErrorKind.Unauthorized, "Member invitations require a Bdgrz user identity.")));
        return issuer.IssueAsync(context, context.Request.TenantId,
            context.Request.EmailAddress, "client_personnel", false,
            context.Request.BuiltInRole, userId, ct);
    }
}
