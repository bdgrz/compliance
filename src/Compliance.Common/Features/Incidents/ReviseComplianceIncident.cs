using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Records the revise compliance incident intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.incidents.revise-compliance-incident", 1)]
public sealed record ReviseComplianceIncident(Uuid TenantId, Uuid ProgramId, Uuid IncidentId, long ExpectedRevision, string Title, string Description, string Impact, string ChangeReason) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
