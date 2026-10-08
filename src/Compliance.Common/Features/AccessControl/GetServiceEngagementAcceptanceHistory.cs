using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.acceptance-history.get", 1)]
public sealed record GetServiceEngagementAcceptanceHistory(Uuid TenantId, Uuid EngagementId)
    : IRequest<IReadOnlyList<ServiceEngagementAcceptanceView>>, IIndependenceAdministrationRequest, ICallable;
