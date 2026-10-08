using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Personal HTTP confirmation of a source-bound omission preview. This is preparer intent, never retirement approval.</summary>
[Discriminator("bdgrz.application_import.omission.freeze", 1)]
public sealed record FreezeApplicationImportOmissionProposal(Uuid TenantId, Uuid BatchId,
    long ExpectedBatchRevision, ulong ExpectedSourcePosition, string ExpectedContentSha256, string Reason)
    : IRequest, IApplicationInventoryWriteRequest, ICallable;
