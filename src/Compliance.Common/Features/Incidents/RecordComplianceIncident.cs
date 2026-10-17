using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Records the record compliance incident intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.incidents.record-compliance-incident", 1)]
public sealed record RecordComplianceIncident(Uuid TenantId, Uuid ProgramId, Uuid IncidentId, string Title, string Description, DateTimeOffset OccurredAt, string Impact, string Source) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
