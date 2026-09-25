using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.get", 1)]
public sealed record GetApplicationImport(Uuid TenantId, Uuid BatchId,
    long? MinimumRevision = null)
    : IRequest<ApplicationImportView>, IApplicationInventoryRequest, ICallable;
