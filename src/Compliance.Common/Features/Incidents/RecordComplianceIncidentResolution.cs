using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Records the record compliance incident resolution intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.incidents.record-compliance-incident-resolution", 1)]
public sealed record RecordComplianceIncidentResolution(Uuid TenantId, Uuid ProgramId, Uuid IncidentId, long Version, string Resolution, DateTimeOffset ResolvedAt, IReadOnlyList<Uuid> EvidenceArtifactIds) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
