using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

sealed class GetTenantProvisioningStatusAuthorizer(IAggregateReader reader)
    : IRequestAuthorizer<GetTenantProvisioningStatus>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<GetTenantProvisioningStatus> context,
        CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Tenant access requires a Bdgrz user identity."));

        var tenant = await reader.HydrateAsync(new Tenant(context.Request.TenantId), ct).ConfigureAwait(false);
        return tenant.WasRegisteredByCreator(userId)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant was not found."));
    }
}
