using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.cancel", 1)]
public sealed record CancelApplicationImport(Uuid TenantId, Uuid BatchId,
    long ExpectedBatchRevision, string Reason)
    : IRequest, IApplicationInventoryRequest, ICallable;
