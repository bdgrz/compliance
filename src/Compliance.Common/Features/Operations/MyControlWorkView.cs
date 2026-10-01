namespace Bdgrz.Compliance.Features.Operations;

/// <summary>The acting member's current and upcoming responsibilities and ordered work.</summary>
public sealed record MyControlWorkView(IReadOnlyList<ControlResponsibilityView> Responsibilities,
    IReadOnlyList<WorkItemView> Items);
