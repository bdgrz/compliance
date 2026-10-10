using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations;

namespace Bdgrz.Compliance.Features.Work;

public sealed record AccountableWorkItemView(Uuid TenantId, Uuid ProgramId, Uuid WorkItemId,
    string Kind, Uuid SourceId, Uuid? ControlId, Uuid? FindingId, string Summary, string Reason,
    DateOnly? DueOn, string? Materiality, string NextAction, string ActionPath,
    OperatingHolder Responsible, OperatingHolder? Backup, Uuid[] Excluded,
    DateTimeOffset CreatedAt, Uuid? RiskId = null)
{
    public Uuid? RestrictedSystemInstanceId { get; init; }

    public static AccountableWorkItemView FromCandidate(Uuid tenantId, Uuid programId,
        WorkCandidate candidate, Uuid? riskId = null) => new(tenantId, programId, candidate.WorkItemId,
        candidate.Kind, candidate.SourceId, candidate.ControlId, candidate.FindingId,
        candidate.Summary, candidate.Reason, candidate.DueOn, candidate.Materiality,
        candidate.NextAction, candidate.ActionPath, candidate.Responsible, candidate.Backup,
        candidate.Excluded.ToArray(), candidate.CreatedAt, riskId)
        {
            RestrictedSystemInstanceId = candidate.RestrictedSystemInstanceId,
        };

    public WorkCandidate ToCandidate() => new(WorkItemId, Kind, SourceId, ControlId, FindingId,
        Summary, Reason, DueOn, Materiality, NextAction, ActionPath, Responsible, Backup,
        Excluded.ToHashSet(), CreatedAt)
    {
        ProgramId = this.ProgramId,
        RestrictedSystemInstanceId = this.RestrictedSystemInstanceId,
    };
}
