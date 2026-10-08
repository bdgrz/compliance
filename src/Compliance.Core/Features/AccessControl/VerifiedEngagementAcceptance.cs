using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Trusted future authorization output; deliberately absent from public request contracts.</summary>
sealed record VerifiedEngagementAcceptance(Uuid TenantId, Uuid EngagementId,
    long ReviewedDraftRevision, Uuid ReviewTaskId, Uuid PartnerStaffMemberId, Uuid PartnerUserId,
    string AuthorityReference, string? PartnerEvaluationReference, Uuid ManagementAcknowledgementId,
    BoundaryVersionView? Boundary, BoundaryDecisionView? BoundaryApproval,
    IReadOnlyList<FirmStaffMemberView> CurrentStaff, FirmStaffMemberView CurrentPartner, long PartnerDutyRevision, DateTimeOffset PartnerAuthorityVerifiedAt);
