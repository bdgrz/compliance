using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.management-acknowledged", 1)]
public sealed record EngagementManagementAcknowledged(Uuid TenantId, Uuid RequestId, long ExpectedSequence,
    EngagementManagementAcknowledgementView Acknowledgement) : DomainEvent;
