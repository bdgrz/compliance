using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.get", 1)]
public sealed record GetApplication(Uuid TenantId, Uuid ApplicationId,
    long? MinimumRevision = null)
    : IRequest<ApplicationView>, IApplicationInventoryRequest, ICallable;
