using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Records the assess compliance incident intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.incidents.assess-compliance-incident", 1)]
public sealed record AssessComplianceIncident(Uuid TenantId, Uuid ProgramId, Uuid IncidentId, long Version, string Significance, string Rationale, IReadOnlyList<Uuid> AffectedRecordIds) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
