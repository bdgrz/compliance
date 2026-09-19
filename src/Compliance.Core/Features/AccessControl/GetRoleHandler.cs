using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetRoleHandler(IRoleDirectoryReader directory) : IRequestHandler<GetRole, RoleView>
{
    public async ValueTask<Result<RoleView>> HandleAsync(IRequestContext<GetRole> context, CancellationToken ct)
    {
        var role = await directory.GetAsync(context.Request.TenantId, context.Request.RoleId, ct);
        return role is null
            ? Result<RoleView>.Failure(new RequestError(RequestErrorKind.NotFound, "The role was not found."))
            : Result<RoleView>.Success(role);
    }
}
