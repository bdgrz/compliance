using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed record ApplicationImportRejectedRow(Uuid RowId, int RowNumber, string? SourceRecordId,
    IReadOnlyList<string> Findings);
