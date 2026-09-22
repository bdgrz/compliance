using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Allows tenant lifecycle commands only from Portia's trusted system actor.</summary>
sealed class TenantLifecycleReactionAuthorizer : IRequestAuthorizer<ITenantLifecycleReactionRequest>
{
    public ValueTask<Result> AuthorizeAsync(
        IRequestContext<ITenantLifecycleReactionRequest> context,
        CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only Portia's trusted system actor may dispatch this request.")));
}
