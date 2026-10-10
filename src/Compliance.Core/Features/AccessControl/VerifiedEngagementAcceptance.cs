using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Server-side source evidence for acceptance. This is returned by a trusted evidence reader and is never
///     accepted from a caller-facing request; the configured reader must prove current partner-duty authority,
///     current source snapshots, and applicable ratified rules for the exact tenant and engagement.
/// </summary>
public sealed record VerifiedEngagementAcceptance(Uuid TenantId, Uuid EngagementId,
    long ReviewedDraftRevision, Uuid ReviewTaskId, Uuid PartnerStaffMemberId, Uuid PartnerUserId,
    string AuthorityReference, string? PartnerEvaluationReference, Uuid ManagementAcknowledgementId,
    BoundaryVersionView? Boundary, BoundaryDecisionView? BoundaryApproval,
    IReadOnlyList<FirmStaffMemberView> CurrentStaff, FirmStaffMemberView CurrentPartner, long PartnerDutyRevision,
    DateTimeOffset PartnerAuthorityVerifiedAt, PartnerIndependenceEvaluationView? PartnerEvaluation = null);

/// <summary>Trusted server-side evidence resolved for one exact draft; never populated from request body fields.</summary>
public sealed record ServiceEngagementAcceptanceEvidence(VerifiedEngagementAcceptance Proof,
    IndependenceRuleVersionView RatifiedRules);
