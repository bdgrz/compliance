using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.list", 1)]
public sealed record ListServiceEngagements(Uuid TenantId)
    : IRequest<IReadOnlyList<ServiceEngagementView>>, IIndependenceAdministrationRequest, ICallable;
