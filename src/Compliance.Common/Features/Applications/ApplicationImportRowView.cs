using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed record ApplicationImportRowView(Uuid TenantId, Uuid BatchId, Uuid RowId,
    int RowNumber, string? SourceRecordId, string? Name, string? Purpose,
    string? OwnerReference, IReadOnlyList<string> ValidationFindings,
    string ProcessingState, Uuid? ApplicationId);
