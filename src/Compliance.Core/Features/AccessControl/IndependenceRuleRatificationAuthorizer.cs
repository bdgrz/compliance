using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Only a personally acting, explicitly designated rule ratifier may approve an exact rules version.</summary>
sealed class IndependenceRuleRatificationAuthorizer(ProfessionalDutyAuthorityReader duties)
    : IRequestAuthorizer<IPersonalRuleRatificationRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IPersonalRuleRatificationRequest> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor))
            return Deny(RequestErrorKind.Forbidden,
                "Professional rule ratification requires a personal HTTP request.");
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Deny(RequestErrorKind.Unauthorized,
                "Professional rule ratification requires a canonical signed-in firm-staff identity.");
        return await duties.ReadCurrentAsync(userId, FirmProfessionalDuty.RuleRatifier, null, ct)
            .ConfigureAwait(false) is not null
            ? Result.Success
            : Deny(RequestErrorKind.Forbidden,
                "Only a currently designated, active firm professional may ratify rules.");
    }

    static Result Deny(RequestErrorKind kind, string message) =>
        Result.Failure(new RequestError(kind, message));
}
