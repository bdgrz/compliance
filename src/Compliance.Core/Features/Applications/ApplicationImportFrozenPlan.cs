namespace Bdgrz.Compliance.Features.Applications;

public sealed record ApplicationImportFrozenPlan(ApplicationImportPlanStarted Start,
    IReadOnlyList<ApplicationImportPlannedRow> Rows, long Revision, string PlanSha256);
