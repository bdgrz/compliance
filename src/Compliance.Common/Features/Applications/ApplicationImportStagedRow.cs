using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed record ApplicationImportStagedRow(Uuid RowId, int RowNumber,
    string? SourceRecordId, string? Name, string? Purpose, string? OwnerReference,
    IReadOnlyList<string> ValidationFindings);
