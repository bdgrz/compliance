using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetTeamHandler(ITeamDirectoryReader directory) : IRequestHandler<GetTeam, TeamView>
{
    public async ValueTask<Result<TeamView>> HandleAsync(IRequestContext<GetTeam> context, CancellationToken ct)
    {
        var team = await directory.GetAsync(context.Request.TenantId, context.Request.TeamId, ct);
        return team is null
            ? Result<TeamView>.Failure(new RequestError(RequestErrorKind.NotFound, "The team was not found."))
            : Result<TeamView>.Success(team);
    }
}
