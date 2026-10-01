using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     The current assignee escalates work to program managers. Escalation adds visibility and
///     never reassigns; an item already escalated, by a member or by the seven-day system rule,
///     cannot be escalated again.
/// </summary>
public sealed class EscalateWorkItemHandler(IAggregateExecutor executor, WorkQueueReader queue,
    TimeProvider clock) : IRequestHandler<EscalateWorkItem, WorkItemDetailView>
{
    public async ValueTask<Result<WorkItemDetailView>> HandleAsync(
        IRequestContext<EscalateWorkItem> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var snapshot = await queue.ReadAsync(request.TenantId, request.ProgramId, actor,
            ControlCadenceSchedule.MaximumDueWithinDays, ct).ConfigureAwait(false);
        if (snapshot.Find(request.WorkItemId) is not { } entry)
            return Result<WorkItemDetailView>.Failure(WorkQueueSnapshot.NotFound());
        if (entry.Item.AssigneeMemberId != actor.MemberId)
            return WorkCommands.Fail(RequestErrorKind.Forbidden,
                "Only the current assignee may escalate this work.");
        if (entry.Item.Escalated)
            return WorkCommands.Fail(RequestErrorKind.Conflict,
                "The work item is already escalated.");
        return await WorkCommands.ExecuteAsync(executor, context, request.TenantId,
            request.ProgramId, entry, snapshot.Today, ledger => ledger.EscalateItem(
                request.WorkItemId, request.ExpectedRevision, request.Reason,
                ActorReference.ForMember(actor.MemberId, actor.Display), clock.GetUtcNow()), ct)
            .ConfigureAwait(false);
    }
}
