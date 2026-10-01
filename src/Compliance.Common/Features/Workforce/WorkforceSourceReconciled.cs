using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

[Discriminator("bdgrz.workforce.source.reconciled", 1)]
public sealed record WorkforceSourceReconciled(Uuid TenantId, Uuid ObservationId, long Revision,
    WorkforceSourceDecision Decision) : DomainEvent;
