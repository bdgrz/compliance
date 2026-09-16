using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class RegisterTenantHandler(IAggregateRepository repository)
    : IRequestHandler<RegisterTenant, TenantRegistration>
{
    public async ValueTask<Result<TenantRegistration>> HandleAsync(
        IRequestContext<RegisterTenant> context,
        CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var ownerUserId))
            return Result<TenantRegistration>.Failure(new RequestError(
                RequestErrorKind.Unauthorized,
                "Tenant registration requires a Bdgrz user identity."));

        var tenant = await repository.HydrateAsync(new Tenant(context.RequestId), ct);
        var result = tenant.Register(ownerUserId, context.Request.Name, context.Request.Slug);
        if (result.IsSuccess)
            await repository.SaveAsync(tenant, context, ct);
        return result;
    }
}
