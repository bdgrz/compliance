using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     Lists one scope of the actor's work in M0-D15 order: due date (undated last), materiality,
///     creation, then work-item ID. Counts are computed from the same items at the same read.
/// </summary>
public sealed class ListWorkHandler(WorkQueueReader queue) : IRequestHandler<ListWork, WorkQueueView>
{
    public const string Mine = "mine";
    public const string Team = "team";
    public const string Unassigned = "unassigned";
    public const string Escalated = "escalated";
    public const string All = "all";

    public async ValueTask<Result<WorkQueueView>> HandleAsync(IRequestContext<ListWork> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var scope = request.Scope ?? Mine;
        if (scope is not (Mine or Team or Unassigned or Escalated or All))
            return Result<WorkQueueView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The scope must be mine, team, unassigned, escalated, or all."));
        if (request.HorizonDays is < 0 or > ControlCadenceSchedule.MaximumDueWithinDays)
            return Result<WorkQueueView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The horizon must be between 0 and 365 days."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var snapshot = await queue.ReadAsync(request.TenantId, request.ProgramId, actor,
            request.HorizonDays ?? WorkQueueReader.DefaultHorizonDays, ct).ConfigureAwait(false);
        var items = snapshot.Entries.Where(entry => scope switch
            {
                Mine => entry.Item.AssigneeMemberId == actor.MemberId,
                Team => entry.ActorInTeam,
                Unassigned => entry.Item.AssigneeMemberId is null && entry.ActorEligible,
                Escalated => entry.Item.Escalated,
                _ => true,
            })
            .Select(static entry => entry.Item)
            .ToArray();
        return Result<WorkQueueView>.Success(new WorkQueueView(scope, snapshot.Today,
            new WorkCountsView(items.Length, items.Count(static item => item.Overdue),
                items.Count(item => item.DueOn == snapshot.Today),
                items.Count(static item => item.Escalated)), items));
    }
}
