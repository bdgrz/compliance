using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class AssignTeamRoleHandler(IAggregateRepository repository) : IRequestHandler<AssignTeamRole>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AssignTeamRole> context, CancellationToken ct)
    {
        var assignment = await repository.HydrateAsync(
            new TeamRole(context.Request.TenantId, context.Request.TeamId, context.Request.RoleId), ct);
        var version = assignment.Version;
        var result = assignment.Assign();
        if (result.IsSuccess && assignment.Version != version)
            await repository.SaveAsync(assignment, context, ct);
        return result;
    }
}
