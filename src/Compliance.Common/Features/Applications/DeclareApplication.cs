using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.declare", 1)]
public sealed record DeclareApplication(Uuid TenantId, string Name, string Purpose,
    string? OwnerReference = null, string? Classification = null)
    : IRequest<ApplicationRegistration>, IApplicationInventoryRequest, ICallable;
