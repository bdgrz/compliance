using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Reads one work item the actor may see; completed or restricted work is not found.</summary>
public sealed class GetWorkItemHandler(WorkQueueReader queue)
    : IRequestHandler<GetWorkItem, WorkItemDetailView>
{
    public ValueTask<Result<WorkItemDetailView>> HandleAsync(
        IRequestContext<GetWorkItem> context, CancellationToken ct)
    {
        var request = context.Request;
        return WorkCommands.RunAsync(queue, context, request.TenantId, request.ProgramId,
            request.WorkItemId, static (_, _, entry) => ValueTask.FromResult(
                Result<WorkItemDetailView>.Success(new WorkItemDetailView(entry.Item,
                    entry.State.History))), ct);
    }
}
