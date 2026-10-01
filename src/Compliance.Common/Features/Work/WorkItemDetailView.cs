namespace Bdgrz.Compliance.Features.Work;

/// <summary>A work item and its assignment history, oldest first.</summary>
public sealed record WorkItemDetailView(WorkQueueItemView Item,
    IReadOnlyList<WorkAssignmentEntryView> History);
