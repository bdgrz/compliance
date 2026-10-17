using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the amend audit population intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-populations.amend-audit-population", 1)]
public sealed record AmendAuditPopulation(Uuid TenantId, Uuid ProgramId, Uuid PopulationId, long ExpectedRevision, string AmendmentReason, IReadOnlyList<Uuid> ChangedRowIds) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
