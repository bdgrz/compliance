using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.retirement_row_frozen", 1)]
public sealed record ApplicationImportRetirementRowFrozen(Uuid TenantId, Uuid BatchId,
    long Revision, ApplicationImportRetirementRow Row) : DomainEvent;
