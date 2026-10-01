namespace Bdgrz.Compliance.Features.Work;

/// <summary>One ordered queue scope with counts that reconcile to its items at the same read.</summary>
public sealed record WorkQueueView(string Scope, DateOnly AsOf, WorkCountsView Counts,
    IReadOnlyList<WorkQueueItemView> Items);
