using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.accept", 1)]
public sealed record AcceptApplicationImport(Uuid TenantId, Uuid BatchId, long ExpectedBatchRevision)
    : IRequest, IApplicationInventoryWriteRequest, ICallable;
