namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     Describes one piece of captured evidence: what it is, where it came from, when it was captured, the period it
///     supports (a point-in-time capture uses the same start and end), and its M0-D16 handling class.
/// </summary>
public sealed record EvidenceArtifactContent(string Title, string? Description, string EvidenceType,
    string Source, DateTimeOffset CapturedAt, DateOnly PeriodStart, DateOnly PeriodEnd,
    string HandlingClass);
