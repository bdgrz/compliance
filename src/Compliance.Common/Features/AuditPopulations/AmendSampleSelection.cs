using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the amend sample selection intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-populations.amend-sample-selection", 1)]
public sealed record AmendSampleSelection(Uuid TenantId, Uuid ProgramId, Uuid SelectionId, long ExpectedRevision, IReadOnlyList<Uuid> PopulationRowIds, string AmendmentReason) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
