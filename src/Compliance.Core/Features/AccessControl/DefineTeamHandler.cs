using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class DefineTeamHandler(IAggregateRepository repository) : IRequestHandler<DefineTeam>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<DefineTeam> context, CancellationToken ct)
    {
        var team = await repository.HydrateAsync(new Team(context.Request.TenantId, context.Request.TeamId), ct);
        var version = team.Version;
        var result = team.Define(context.Request.Name);
        if (result.IsSuccess && team.Version != version)
            await repository.SaveAsync(team, context, ct);
        return result;
    }
}
