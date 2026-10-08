using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.progress", 1)]
public sealed record GetApplicationImportProgress(Uuid TenantId, Uuid BatchId, long? MinimumRevision = null)
    : IRequest<ApplicationImportProgress>, IApplicationInventoryRequest, ICallable;
