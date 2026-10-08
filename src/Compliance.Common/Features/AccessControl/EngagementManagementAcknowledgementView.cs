using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record EngagementManagementAcknowledgementView(Uuid TenantId, Uuid EngagementId,
    Uuid AcknowledgementId, long EngagementRevision, IReadOnlyList<Uuid> CompleteServiceRecordIds,
    string Statement, Uuid UserId, ActorReference Actor, DateTimeOffset RecordedAt);
