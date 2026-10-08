using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.changed", 1)]
public sealed record ServiceEngagementMutationRecorded(Uuid TenantId, Uuid RequestId, long ExpectedSequence,
    string Intent, ServiceEngagementView Engagement) : DomainEvent;
