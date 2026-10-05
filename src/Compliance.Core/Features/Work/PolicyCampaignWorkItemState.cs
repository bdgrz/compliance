using Bdgrz.Compliance.Features.PolicyDistribution;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record PolicyCampaignWorkCampaignState(Uuid TenantId, Uuid ProgramId,
    Uuid CampaignId, CampaignSubject Subject, Uuid OwnerMemberId, DateTimeOffset LaunchedAt,
    bool IsClosed);

public sealed record PolicyCampaignWorkParticipantState(Uuid TenantId, Uuid ProgramId,
    Uuid CampaignId, Uuid PersonId, string DisplayName, DateOnly DueOn, bool IsActive,
    DateTimeOffset? AcknowledgedAt = null, DateOnly? CompletedOn = null,
    DateTimeOffset? CompletionRecordedAt = null, DateTimeOffset? WaiverApprovedAt = null,
    DateOnly? WaiverExpiresOn = null);
