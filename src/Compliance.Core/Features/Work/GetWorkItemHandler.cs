using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Reads one work item the actor may see; completed or restricted work is not found.</summary>
public sealed class GetWorkItemHandler(WorkQueueReader queue)
    : IRequestHandler<GetWorkItem, WorkItemDetailView>
{
    public async ValueTask<Result<WorkItemDetailView>> HandleAsync(
        IRequestContext<GetWorkItem> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var snapshot = await queue.ReadAsync(request.TenantId, request.ProgramId, actor,
            ControlCadenceSchedule.MaximumDueWithinDays, ct).ConfigureAwait(false);
        return snapshot.Find(request.WorkItemId) is { } entry
            ? Result<WorkItemDetailView>.Success(new WorkItemDetailView(entry.Item,
                entry.State.History))
            : Result<WorkItemDetailView>.Failure(WorkQueueSnapshot.NotFound());
    }
}
