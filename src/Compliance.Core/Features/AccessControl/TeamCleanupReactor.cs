using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Removes a deleted team's members so no orphaned <see cref="TeamMember" /> assignment survives
///     the team itself. Not authoritative on its own — a member removed concurrently with the
///     deletion is simply a no-op on <see cref="RemoveTeamMember" />; the aggregate still owns
///     correctness.
/// </summary>
public sealed partial class TeamCleanupReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus,
    ITeamMemberDirectoryReader members)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("rbac-teams"), "TeamCleanup"),
      IReactorHandler<TeamDeleted>
{
    public async ValueTask HandleAsync(IReactorContext<TeamDeleted> context, CancellationToken ct)
    {
        var tenantId = context.Trigger.TenantId;
        var teamId = context.Trigger.TeamId;
        string? cursor = null;
        do
        {
            var page = await members.ListAsync(tenantId, teamId, 200, cursor, null, descending: false, ct)
                .ConfigureAwait(false);
            foreach (var member in page.Items)
            {
                await bus.SendReactionAsync(new RemoveTeamMember(tenantId, teamId, member.MemberId), context, ct)
                    .ConfigureAwait(false);
            }

            cursor = page.NextCursor;
        } while (cursor is not null);
    }
}
