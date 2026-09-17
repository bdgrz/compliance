using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed partial class TeamMemberDirectoryProjector(ITeamMemberDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "TeamMemberDirectory"),
      IProjectorHandler<TeamMemberAssigned>,
      IProjectorHandler<TeamMemberRemoved>
{
    public ValueTask HandleAsync(TeamMemberAssigned ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TeamMemberRemoved ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
