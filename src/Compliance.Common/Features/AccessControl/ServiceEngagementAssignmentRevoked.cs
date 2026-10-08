using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.assignment-revoked", 1)]
public sealed record ServiceEngagementAssignmentRevoked(Uuid TenantId, Uuid RequestId, long ExpectedSequence,
    Uuid EngagementId, Uuid? StaffMemberId, string Reason, string Intent, ActorReference Actor,
    DateTimeOffset RecordedAt) : DomainEvent;
