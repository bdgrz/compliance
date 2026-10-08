using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A causal lifecycle receipt that supplies no refreshed professional authority.</summary>
public sealed record AssignmentIndependenceReevaluationView(Uuid ReevaluationId, Uuid TenantId,
    Uuid CausalRequestId, long SourceSequence, Uuid EngagementId, Uuid? StaffMemberId,
    string CauseCode, string SourceReason, string SourceIntent,
    ServiceEngagementAcceptanceView PreviousAcceptance, string PreviousAcceptanceSha256,
    ServiceEngagementAcceptanceView ResultingAcceptance, string ResultingAcceptanceSha256,
    IReadOnlyList<HistoricalEngagementAssignmentView> CanonicalAssignmentHistory,
    string State, bool ProductionAcceptanceBlocked, ActorReference SourceActor, DateTimeOffset RecordedAt);
