using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.accept", 1)]
public sealed record AcceptServiceEngagement(Uuid TenantId, Uuid EngagementId, long ExpectedSequence)
    : IRequest<ServiceEngagementAcceptanceView>, IServiceEngagementAcceptanceRequest, ICallable;
