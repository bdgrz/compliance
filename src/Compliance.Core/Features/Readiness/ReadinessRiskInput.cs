using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>One program risk with its evaluation status as of the assessment time.</summary>
public sealed record ReadinessRiskInput(Uuid RiskId, string Identifier, long Revision,
    string EvaluationStatus);
