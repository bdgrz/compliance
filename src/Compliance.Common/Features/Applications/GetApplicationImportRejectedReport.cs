using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.rejected_report", 1)]
public sealed record GetApplicationImportRejectedReport(Uuid TenantId, Uuid BatchId, long? MinimumRevision = null)
    : IRequest<ApplicationImportRejectedReport>, IApplicationInventoryRequest, ICallable;
