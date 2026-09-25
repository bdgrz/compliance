using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.revision.get", 1)]
public sealed record GetApplicationRevision(Uuid TenantId, Uuid ApplicationId, long Revision)
    : IRequest<ApplicationRevisionView>, IApplicationInventoryRequest, ICallable;
