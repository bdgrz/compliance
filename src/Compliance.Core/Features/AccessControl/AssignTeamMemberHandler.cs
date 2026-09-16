using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class AssignTeamMemberHandler(IAggregateRepository repository) : IRequestHandler<AssignTeamMember>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AssignTeamMember> context, CancellationToken ct)
    {
        var assignment = await repository.HydrateAsync(
            new TeamMember(context.Request.TenantId, context.Request.TeamId, context.Request.MemberId), ct);
        var version = assignment.Version;
        var result = assignment.Assign();
        if (result.IsSuccess && assignment.Version != version)
            await repository.SaveAsync(assignment, context, ct);
        return result;
    }
}
