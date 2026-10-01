using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Allows operations reaction commands only from Portia's trusted system actor.</summary>
sealed class OperationsReactionAuthorizer : IRequestAuthorizer<IOperationsReactionRequest>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IOperationsReactionRequest> context,
        CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only Portia's trusted system actor may dispatch this request.")));
}
