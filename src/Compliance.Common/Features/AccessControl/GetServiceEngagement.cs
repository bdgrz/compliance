using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.get", 1)]
public sealed record GetServiceEngagement(Uuid TenantId, Uuid EngagementId)
    : IRequest<ServiceEngagementView>, IIndependenceAdministrationRequest, ICallable;
