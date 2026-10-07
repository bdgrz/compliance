using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Read-only source reconciliation; no preview row grants acceptance authority.</summary>
public sealed record ApplicationImportPreviewRow(Uuid TenantId, Uuid BatchId, Uuid RowId,
    int RowNumber, string? SourceRecordId, string? Name, string? Purpose,
    string? OwnerReference, IReadOnlyList<string> ValidationFindings,
    string MatchState, IReadOnlyList<Uuid> CandidateApplicationIds,
    IReadOnlyList<string> ChangedFields, IReadOnlyList<string> AcceptanceBlockers)
{
    public ApplicationImportCorrelationView? Correlation { get; init; }
}
