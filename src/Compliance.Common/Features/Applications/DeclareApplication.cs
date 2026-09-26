using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.declare", 1)]
public sealed record DeclareApplication(Uuid TenantId, string Name, string Purpose,
    string? OwnerReference = null, string? Classification = null, Uuid? SystemOwnerPersonId = null,
    Uuid? AccessOwnerPersonId = null)
    : IRequest<ApplicationRegistration>, IApplicationInventoryRequest, ICallable;
