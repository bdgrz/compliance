using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class RecordDirectoryIndependenceReevaluationAuthorizer : IRequestAuthorizer<RecordDirectoryIndependenceReevaluation>
{
    internal static string? Process(IRequestContext context)
    {
        if (!RequestActor.IsSystem(context.Actor) || context.Invocation is not DirectInvocation)
            return null;
        var subjects = context.Actor.FindAll(ClaimTypes.NameIdentifier).ToArray();
        return subjects.Length == 1 && subjects[0].Issuer == "bdgrz.system" &&
            subjects[0].Value is "reactor:FirmStaffStatusReevaluationV1" or "reactor:AcceptedStaffDirectoryReconciliationV1"
                ? subjects[0].Value : null;
    }

    public ValueTask<Result> AuthorizeAsync(IRequestContext<RecordDirectoryIndependenceReevaluation> context, CancellationToken ct) =>
        ValueTask.FromResult(Process(context) is not null ? Result.Success : Result.Failure(
            new RequestError(RequestErrorKind.Forbidden, "Only the named internal reconciliation workers may record directory receipts.")));
}
