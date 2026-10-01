using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>The current assignee delegates work to another member the source workflow would accept.</summary>
public sealed class DelegateWorkItemHandler(IAggregateExecutor executor, WorkQueueReader queue,
    TimeProvider clock) : IRequestHandler<DelegateWorkItem, WorkItemDetailView>
{
    public ValueTask<Result<WorkItemDetailView>> HandleAsync(
        IRequestContext<DelegateWorkItem> context, CancellationToken ct)
    {
        var request = context.Request;
        return WorkCommands.RunAsync(queue, context, request.TenantId, request.ProgramId,
            request.WorkItemId, async (actor, snapshot, entry) =>
            {
                if (entry.Item.AssigneeMemberId != actor.MemberId)
                    return WorkCommands.Fail(RequestErrorKind.Forbidden,
                        "Only the current assignee may delegate this work.");
                if (string.IsNullOrWhiteSpace(request.Reason))
                    return WorkCommands.Fail(RequestErrorKind.Validation,
                        "A delegation needs a reason.");
                if (request.AssigneeMemberId == actor.MemberId ||
                    !await queue.IsEligibleAsync(request.TenantId, entry.Candidate,
                        request.AssigneeMemberId, ct).ConfigureAwait(false))
                    return WorkCommands.Fail(RequestErrorKind.Validation,
                        "The source workflow would not accept this member for the work, including its separation of duties.");
                return await WorkCommands.ExecuteAsync(executor, context, request.TenantId,
                    request.ProgramId, entry, snapshot.Today, ledger => ledger.AssignTo(
                        request.WorkItemId, request.ExpectedRevision, WorkAssignmentLedger.Delegate,
                        request.AssigneeMemberId, actor.MemberId, request.Reason,
                        ActorReference.ForMember(actor.MemberId, actor.Display),
                        clock.GetUtcNow()), ct).ConfigureAwait(false);
            }, ct);
    }
}
