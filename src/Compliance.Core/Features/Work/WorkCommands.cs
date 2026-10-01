using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Records one accountability change and returns the item as it now reads.</summary>
static class WorkCommands
{
    public static async ValueTask<Result<WorkItemDetailView>> ExecuteAsync<TRequest>(
        IAggregateExecutor executor, IRequestContext<TRequest> context, Uuid tenantId,
        Uuid programId, WorkQueueEntry entry, DateOnly today,
        Func<WorkAssignmentLedger, CommandFailure?> command, CancellationToken ct)
        where TRequest : IRequestBase =>
        await executor.ExecuteAsync(new WorkAssignmentLedger(tenantId, programId), ledger =>
        {
            var failure = command(ledger);
            var state = ledger.Read(entry.Candidate.WorkItemId);
            // A newly recorded assignee was validated as eligible; an unchanged one keeps the
            // eligibility it had when the queue was read.
            var recordedEligible = state.AssigneeMemberId is { } assignee &&
                                   (assignee != entry.State.AssigneeMemberId ||
                                    assignee == entry.Item.AssigneeMemberId);
            return CommandFailureRequestAdapter.ToOutcome(failure, new WorkItemDetailView(
                WorkQueueReader.Compose(entry.Candidate, state, recordedEligible, today),
                state.History));
        }, context, ct).ConfigureAwait(false);

    public static Result<WorkItemDetailView> Fail(RequestErrorKind kind, string message) =>
        Result<WorkItemDetailView>.Failure(new RequestError(kind, message));
}
