using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>One scored assessment; the score is derived as likelihood times impact.</summary>
public sealed record RiskAssessmentView(Uuid AssessmentId, string Phase, Uuid MethodVersionId,
    long MethodVersion, int Likelihood, int Impact, int Score, string Rationale,
    ActorReference Assessor, DateTimeOffset AssessedAt);
