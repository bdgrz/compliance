using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A personal, immutable decision bound to one exact draft, ratified rules and complete client service history.</summary>
public sealed record PartnerIndependenceEvaluationView(Uuid EvaluationId, Uuid TenantId,
    Uuid EngagementId, long DraftRevision, long RuleVersion, string RuleContentDigest,
    string ServiceHistoryDigest, IReadOnlyList<NonattestServiceView> CompleteServiceHistory,
    IReadOnlyList<Uuid> ConsideredServiceRecordIds, string DecisionCode, string Outcome, string Rationale,
    Uuid PartnerStaffMemberId, Uuid PartnerUserId, long PartnerDirectoryStaffRevision,
    Uuid DutyDesignationId, long PartnerDutyRevision, string DutySourceReference,
    ActorReference Actor, DateTimeOffset RecordedAt);
