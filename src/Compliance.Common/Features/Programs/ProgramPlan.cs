namespace Bdgrz.Compliance.Features.Programs;

public sealed record ProgramPlan(DateOnly? TargetReadinessDate, DateOnly? TargetTypeIAsOfDate,
    DateOnly? TargetTypeIIStartDate, DateOnly? TargetTypeIIEndDate,
    string? ReadinessAdvisor, string? AuditFirm);
