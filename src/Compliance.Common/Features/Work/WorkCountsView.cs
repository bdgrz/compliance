namespace Bdgrz.Compliance.Features.Work;

/// <summary>Counts of the items in one queue scope.</summary>
public sealed record WorkCountsView(int Total, int Overdue, int DueToday, int Escalated);
