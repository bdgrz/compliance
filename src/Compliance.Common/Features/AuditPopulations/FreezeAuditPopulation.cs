using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the freeze audit population intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-populations.freeze-audit-population", 1)]
public sealed record FreezeAuditPopulation(Uuid TenantId, Uuid ProgramId, Uuid PopulationId, long ExpectedRevision, string FreezeRationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
