using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Provisional only: no source-claim bindings exist in this staging slice.</summary>
public sealed record ApplicationImportPreviewRow(Uuid TenantId, Uuid BatchId, Uuid RowId,
    int RowNumber, string? SourceRecordId, string? Name, string? Purpose,
    string? OwnerReference, IReadOnlyList<string> ValidationFindings,
    string MatchState, IReadOnlyList<Uuid> CandidateApplicationIds,
    IReadOnlyList<string> ChangedFields, IReadOnlyList<string> AcceptanceBlockers);
