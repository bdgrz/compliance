using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Internal system command; the ledger supplies all effect content and attribution.</summary>
[Discriminator("bdgrz.application_import.apply_effect", 1)]
public sealed record ApplyApplicationImportEffect(Uuid TenantId, Uuid BatchId,
    Uuid RowId, Uuid ApplicationId) : IRequest;
