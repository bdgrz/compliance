namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Support offered for an attestation, corrective action, or closure. Kind is artifact,
///     record, or external. Resolution is set by the server: references stay unresolved until
///     governed evidence capture exists, and the exact text recorded is preserved.
/// </summary>
public sealed record EvidenceReference(int? ExpectedEvidenceIndex, string Kind, string Reference,
    string? Description = null, string? Resolution = null);
