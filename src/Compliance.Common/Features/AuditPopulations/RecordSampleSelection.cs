using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the record sample selection intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-populations.record-sample-selection", 1)]
public sealed record RecordSampleSelection(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId, Uuid PopulationId, long PopulationVersion, IReadOnlyList<Uuid> PopulationRowIds, string Source, string ExternalReference) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
