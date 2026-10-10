using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Operators record source-backed platform metadata in person; this permission grants no professional duty.</summary>
sealed class ProfessionalDutyAdministrationAuthorizer(IPlatformOperatorAccess operators)
    : IRequestAuthorizer<IProfessionalDutyAdministrationRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IProfessionalDutyAdministrationRequest> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor))
            return Deny(RequestErrorKind.Forbidden,
                "Professional-duty metadata requires a personal HTTP request.");
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Deny(RequestErrorKind.Unauthorized,
                "Professional-duty metadata requires a canonical signed-in operator.");
        return await operators.IsOperatorAsync(userId, ct).ConfigureAwait(false)
            ? Result.Success
            : Deny(RequestErrorKind.Forbidden, "Only a current platform operator may record professional-duty metadata.");
    }

    static Result Deny(RequestErrorKind kind, string message) =>
        Result.Failure(new RequestError(kind, message));
}
