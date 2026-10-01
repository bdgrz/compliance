namespace Bdgrz.Compliance.Features.Operations;

/// <summary>The performer-supplied content of one attestation version.</summary>
public sealed record AttestationInput(string Result, DateTimeOffset PerformedAt,
    DateOnly? CoveredFrom, DateOnly? CoveredUntil, string? Notes, string? Rationale,
    IReadOnlyList<EvidenceReference> Evidence, OperatingHolder PerformedBy);
