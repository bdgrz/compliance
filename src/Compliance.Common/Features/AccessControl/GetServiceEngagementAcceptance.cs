using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.acceptance.get", 1)]
public sealed record GetServiceEngagementAcceptance(Uuid TenantId, Uuid EngagementId)
    : IRequest<ServiceEngagementAcceptanceView>, IIndependenceAdministrationRequest, ICallable;
