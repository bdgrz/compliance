using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class ExecuteApplicationImportAuthorizer : IRequestAuthorizer<ExecuteApplicationImport>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<ExecuteApplicationImport> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor) ? Result.Success : Result.Failure(
            new RequestError(RequestErrorKind.Forbidden, "Only the trusted system actor may execute an import.")));
}
