using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.incidents.get-compliance-impact-assessment", 1)]
public sealed record GetComplianceImpactAssessment(Uuid TenantId, Uuid ProgramId, Uuid ComplianceImpactAssessmentId)
    : IRequest<ComplianceImpactAssessmentView>, IProgramReadRequest, ICallable;
