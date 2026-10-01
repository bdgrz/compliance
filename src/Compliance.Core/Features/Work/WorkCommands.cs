using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Resolves the one work item a command targets and records its accountability change.</summary>
static class WorkCommands
{
    /// <summary>Runs <paramref name="command" /> against the visible item, or returns not found.</summary>
    public static async ValueTask<Result<WorkItemDetailView>> RunAsync<TRequest>(
        WorkQueueReader queue, IRequestContext<TRequest> context, Uuid tenantId, Uuid programId,
        Uuid workItemId,
        Func<OperationsActor, WorkQueueSnapshot, WorkQueueEntry,
            ValueTask<Result<WorkItemDetailView>>> command, CancellationToken ct)
        where TRequest : IRequestBase
    {
        var actor = OperationsActor.From(context.Actor, tenantId);
        var found = await queue.FindAsync(tenantId, programId, actor, workItemId, ct)
            .ConfigureAwait(false);
        if (!found.IsSuccess)
            return Result<WorkItemDetailView>.Failure(found.Error!);
        var (snapshot, entry) = found.Value;
        return await command(actor, snapshot, entry).ConfigureAwait(false);
    }

    public static async ValueTask<Result<WorkItemDetailView>> ExecuteAsync<TRequest>(
        IAggregateExecutor executor, IRequestContext<TRequest> context, Uuid tenantId,
        Uuid programId, WorkQueueEntry entry, DateOnly today,
        Func<WorkAssignmentLedger, CommandFailure?> command, CancellationToken ct)
        where TRequest : IRequestBase =>
        await executor.ExecuteAsync(new WorkAssignmentLedger(tenantId, programId), ledger =>
        {
            var failure = command(ledger);
            var state = ledger.Read(entry.Candidate.WorkItemId);
            // A newly recorded assignee was validated as eligible; otherwise accountability is
            // unchanged from the read.
            var assignee = state.AssigneeMemberId != entry.State.AssigneeMemberId
                ? state.AssigneeMemberId
                : entry.Item.AssigneeMemberId;
            return CommandFailureRequestAdapter.ToOutcome(failure, new WorkItemDetailView(
                WorkQueueReader.Compose(entry.Candidate, state, assignee, today),
                state.History));
        }, context, ct).ConfigureAwait(false);

    public static Result<WorkItemDetailView> Fail(RequestErrorKind kind, string message) =>
        Result<WorkItemDetailView>.Failure(new RequestError(kind, message));
}
