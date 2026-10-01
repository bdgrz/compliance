using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Where a finding came from, with the source wording preserved verbatim. Kind is manual,
///     readiness_gap, control_occurrence, occurrence_review, evaluation_deviation, incident,
///     consultant_observation, or examination_item.
/// </summary>
public sealed record FindingSource(string Kind, Uuid? RecordId, string? Version,
    string SourceText);
