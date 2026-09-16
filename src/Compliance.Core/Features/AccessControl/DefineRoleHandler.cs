using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class DefineRoleHandler(IAggregateRepository repository) : IRequestHandler<DefineRole>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<DefineRole> context, CancellationToken ct)
    {
        var role = await repository.HydrateAsync(new Role(context.Request.TenantId, context.Request.RoleId), ct);
        var version = role.Version;
        var result = role.Define(context.Request.Name);
        if (result.IsSuccess && role.Version != version)
            await repository.SaveAsync(role, context, ct);
        return result;
    }
}
