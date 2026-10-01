using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     A program manager (Compliance Lead or Org Admin) assigns or reassigns work. The target must
///     be a member the source workflow would accept, including its separation of duties.
/// </summary>
public sealed class AssignWorkItemHandler(IAggregateExecutor executor, WorkQueueReader queue,
    TimeProvider clock) : IRequestHandler<AssignWorkItem, WorkItemDetailView>
{
    public ValueTask<Result<WorkItemDetailView>> HandleAsync(
        IRequestContext<AssignWorkItem> context, CancellationToken ct)
    {
        var request = context.Request;
        return WorkCommands.RunAsync(queue, context, request.TenantId, request.ProgramId,
            request.WorkItemId, async (actor, snapshot, entry) =>
            {
                if (!snapshot.ActorManages)
                    return WorkCommands.Fail(RequestErrorKind.Forbidden,
                        "Only a program manager may assign or reassign work.");
                if (entry.Item.AssigneeMemberId == request.AssigneeMemberId)
                    return WorkCommands.Fail(RequestErrorKind.Conflict,
                        "The work item is already assigned to this member.");
                if (!await queue.IsEligibleAsync(request.TenantId, entry.Candidate,
                        request.AssigneeMemberId, ct).ConfigureAwait(false))
                    return WorkCommands.Fail(RequestErrorKind.Validation,
                        "The source workflow would not accept this member for the work, including its separation of duties.");
                var previous = entry.Item.AssigneeMemberId;
                return await WorkCommands.ExecuteAsync(executor, context, request.TenantId,
                    request.ProgramId, entry, snapshot.Today, ledger => ledger.AssignTo(
                        request.WorkItemId, request.ExpectedRevision,
                        previous is null ? WorkAssignmentLedger.Assign : WorkAssignmentLedger.Reassign,
                        request.AssigneeMemberId, previous, request.Reason,
                        ActorReference.ForMember(actor.MemberId, actor.Display),
                        clock.GetUtcNow()), ct).ConfigureAwait(false);
            }, ct);
    }
}
