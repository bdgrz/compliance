using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record ServiceEngagementView(Uuid TenantId, Uuid EngagementId, long Revision,
    ServiceEngagementDraftContent Content, string Status, IReadOnlyList<ServiceEngagementStaffProposalView> Staff,
    ActorReference Actor, DateTimeOffset RecordedAt, bool ProfessionalAccessGranted);
