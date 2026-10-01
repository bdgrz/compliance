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
    public ValueTask<Result<WorkItemDetailView>> HandleAsync(
        IRequestContext<EscalateWorkItem> context, CancellationToken ct)
    {
        var request = context.Request;
        return WorkCommands.RunAsync(queue, context, request.TenantId, request.ProgramId,
            request.WorkItemId, async (actor, snapshot, entry) =>
            {
                if (entry.Item.AssigneeMemberId != actor.MemberId)
                    return WorkCommands.Fail(RequestErrorKind.Forbidden,
                        "Only the current assignee may escalate this work.");
                if (entry.Item.Escalated)
                    return WorkCommands.Fail(RequestErrorKind.Conflict,
                        "The work item is already escalated.");
                return await WorkCommands.ExecuteAsync(executor, context, request.TenantId,
                    request.ProgramId, entry, snapshot.Today, ledger => ledger.EscalateItem(
                        request.WorkItemId, request.ExpectedRevision, request.Reason,
                        ActorReference.ForMember(actor.MemberId, actor.Display),
                        clock.GetUtcNow()), ct).ConfigureAwait(false);
            }, ct);
    }
}
