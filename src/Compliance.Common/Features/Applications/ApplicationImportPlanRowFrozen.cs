using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.plan_row_frozen", 1)]
public sealed record ApplicationImportPlanRowFrozen(Uuid TenantId, Uuid BatchId,
    long Revision, ApplicationImportPlannedRow Row) : DomainEvent;
