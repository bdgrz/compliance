using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class RegisterTenantOwnerHandler(IAggregateRepository repository)
    : IRequestHandler<RegisterTenantOwner>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RegisterTenantOwner> context, CancellationToken ct)
    {
        var owner = await repository.HydrateAsync(
            new TenantOwner(context.Request.TenantId, context.Request.UserId), ct);
        var version = owner.Version;
        var result = owner.Register();
        if (result.IsSuccess && owner.Version != version)
            await repository.SaveAsync(owner, context, ct);
        return result;
    }
}
