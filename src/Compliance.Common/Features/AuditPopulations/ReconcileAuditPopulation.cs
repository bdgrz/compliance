using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the reconcile audit population intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-populations.reconcile-audit-population", 1)]
public sealed record ReconcileAuditPopulation(Uuid TenantId, Uuid ProgramId, Uuid PopulationId, long ExpectedRevision, string ReconciliationKey) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
