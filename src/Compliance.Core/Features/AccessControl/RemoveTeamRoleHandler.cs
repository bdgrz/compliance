using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class RemoveTeamRoleHandler(IAggregateExecutor executor) : IRequestHandler<RemoveTeamRole>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RemoveTeamRole> context, CancellationToken ct) =>
        context.Request.TeamId == BuiltInRbac.AdministratorsTeamId(context.Request.TenantId) &&
        context.Request.RoleId == BuiltInRbac.TenantAdministrationRoleId(context.Request.TenantId)
            ? ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The Administrators team keeps the Tenant Administration role.")))
            : executor.ExecuteAsync(
            new TeamRole(context.Request.TenantId, context.Request.TeamId, context.Request.RoleId),
            assignment => AggregateOutcome.CommitOnSuccess(assignment.Remove()),
            context, ct);
}
