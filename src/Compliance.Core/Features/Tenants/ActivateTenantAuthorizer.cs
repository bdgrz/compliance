using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

sealed class ActivateTenantAuthorizer : IRequestAuthorizer<ActivateTenant>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<ActivateTenant> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only tenant bootstrap reactions may activate a tenant.")));
}
