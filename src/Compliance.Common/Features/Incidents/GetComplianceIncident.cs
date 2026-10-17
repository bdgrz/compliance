using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.incidents.get-compliance-incident", 1)]
public sealed record GetComplianceIncident(Uuid TenantId, Uuid ProgramId, Uuid ComplianceIncidentId)
    : IRequest<ComplianceIncidentView>, IProgramReadRequest, ICallable;
