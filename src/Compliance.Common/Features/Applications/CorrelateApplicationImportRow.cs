using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.correlate_row", 1)]
public sealed record CorrelateApplicationImportRow(Uuid TenantId, Uuid BatchId,
    Uuid RowId, long ExpectedBatchRevision, string Decision, Uuid? ApplicationId,
    long? ExpectedApplicationRevision, string Reason)
    : IRequest, IApplicationInventoryWriteRequest, ICallable;
