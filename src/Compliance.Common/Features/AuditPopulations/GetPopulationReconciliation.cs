using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.audit-populations.get-population-reconciliation", 1)]
public sealed record GetPopulationReconciliation(Uuid TenantId, Uuid ProgramId, Uuid PopulationReconciliationId)
    : IRequest<PopulationReconciliationView>, IProgramReadRequest, ICallable;
