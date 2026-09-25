namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Untrusted tenant-supplied source row. Staging does not create an Application.</summary>
public sealed record ApplicationImportInputRow(string? SourceRecordId, string? Name,
    string? Purpose, string? OwnerReference);
