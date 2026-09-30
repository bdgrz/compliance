using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>A personal, time-bounded acceptance; status is evaluated when read.</summary>
public sealed record RiskAcceptanceView(Uuid AcceptanceId, Uuid ResidualAssessmentId,
    int ResidualScore, int? AppetiteThreshold, Uuid ApproverMemberId, ActorReference Approver,
    string ApproverAuthority, string Rationale, DateTimeOffset AcceptedAt,
    DateTimeOffset ExpiresAt, string Status = "active");
