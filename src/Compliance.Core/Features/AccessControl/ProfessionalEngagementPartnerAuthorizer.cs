using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Restricts partner workflow requests to personal HTTP calls by the current designated client partner.</summary>
sealed class ProfessionalEngagementPartnerAuthorizer(ProfessionalDutyAuthorityReader duties,
    ITenantActivity tenants) : IRequestAuthorizer<IPersonalEngagementPartnerRequest>
{
    public async ValueTask<Result> AuthorizeAsync(
        IRequestContext<IPersonalEngagementPartnerRequest> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor))
            return Deny(RequestErrorKind.Forbidden,
                "Professional partner review requires a personal HTTP request.");
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Deny(RequestErrorKind.Unauthorized,
                "Professional partner review requires a canonical signed-in firm-staff identity.");
        var tenantId = context.Request.TenantId;
        if (tenantId == Uuid.Empty || !await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Deny(RequestErrorKind.Forbidden, "The client tenant is not active.");
        return await duties.ReadCurrentAsync(userId, FirmProfessionalDuty.EngagementPartner, tenantId, ct)
            .ConfigureAwait(false) is not null
            ? Result.Success
            : Deny(RequestErrorKind.Forbidden,
                "Only the current, active partner designated for this client may review the engagement.");
    }

    static Result Deny(RequestErrorKind kind, string message) =>
        Result.Failure(new RequestError(kind, message));
}
