using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class AssignRolePermissionHandler(IAggregateRepository repository)
    : IRequestHandler<AssignRolePermission>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AssignRolePermission> context, CancellationToken ct)
    {
        var assignment = await repository.HydrateAsync(
            new RolePermission(context.Request.TenantId, context.Request.RoleId, context.Request.Permission), ct);
        var version = assignment.Version;
        var result = assignment.Assign();
        if (result.IsSuccess && assignment.Version != version)
            await repository.SaveAsync(assignment, context, ct);
        return result;
    }
}
