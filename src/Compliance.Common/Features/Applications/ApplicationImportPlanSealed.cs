using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.plan_sealed", 1)]
public sealed record ApplicationImportPlanSealed(Uuid TenantId, Uuid BatchId,
    long Revision, string PlanSha256) : DomainEvent;
