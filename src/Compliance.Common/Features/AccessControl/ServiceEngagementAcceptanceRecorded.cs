using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.acceptance-recorded", 1)]
public sealed record ServiceEngagementAcceptanceRecorded(Uuid TenantId, Uuid RequestId,
    long ExpectedSequence, string Intent, ServiceEngagementAcceptanceView Acceptance) : DomainEvent;
