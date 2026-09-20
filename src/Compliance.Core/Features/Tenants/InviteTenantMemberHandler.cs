using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class InviteTenantMemberHandler(TenantInvitationIssuer issuer,
    IAggregateReader reader) : IRequestHandler<InviteTenantMember>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<InviteTenantMember> context, CancellationToken ct)
    {
        var tenant = await reader.HydrateAsync(new Tenant(context.Request.TenantId), ct).ConfigureAwait(false);
        if (!tenant.IsRegistered)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant does not exist."));

        var invitedBy = RequestActor.IsSystem(context.Actor)
            ? tenant.OperatorUserId
            : UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId)
                ? userId
                : Uuid.Empty;
        return await issuer.IssueAsync(context, context.Request.TenantId,
            context.Request.EmailAddress, context.Request.Affiliation,
            context.Request.Administrator, null, invitedBy, ct).ConfigureAwait(false);
    }
}
