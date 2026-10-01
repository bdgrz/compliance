using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     One program risk resolved as of the assessment time: when it was created, the revision in
///     force, and its evaluation status from assessments, treatment, and acceptances recorded by then.
/// </summary>
public sealed record ReadinessRiskInput(Uuid RiskId, string Identifier, DateTimeOffset CreatedAt,
    long Revision, string EvaluationStatus);
