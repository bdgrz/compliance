using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.history.get", 1)]
public sealed record GetServiceEngagementHistory(Uuid TenantId, Uuid EngagementId)
    : IRequest<IReadOnlyList<ServiceEngagementView>>, IIndependenceAdministrationRequest, ICallable;
