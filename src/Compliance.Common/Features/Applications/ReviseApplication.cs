using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.revise", 1)]
public sealed record ReviseApplication(Uuid TenantId, Uuid ApplicationId, long ExpectedRevision,
    string Name, string Purpose, string? OwnerReference, string? Classification = null, Uuid? SystemOwnerPersonId = null,
    Uuid? AccessOwnerPersonId = null)
    : IRequest, IApplicationInventoryRequest, ICallable;
