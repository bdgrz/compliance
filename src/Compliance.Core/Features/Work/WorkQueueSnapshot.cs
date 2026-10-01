using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>The actor's visible, ordered work as of one read, and whether they manage the program.</summary>
sealed record WorkQueueSnapshot(DateOnly Today, bool ActorManages,
    IReadOnlyList<WorkQueueEntry> Entries)
{
    public WorkQueueEntry? Find(Uuid workItemId) =>
        Entries.FirstOrDefault(entry => entry.Item.WorkItemId == workItemId);

    public static RequestError NotFound() =>
        new(RequestErrorKind.NotFound, "The work item was not found.");
}
