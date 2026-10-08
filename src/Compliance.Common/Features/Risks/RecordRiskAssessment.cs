using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.assessment.record", 1)]
public sealed record RecordRiskAssessment(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long ExpectedRevision, long MethodVersion, string Phase, int Likelihood, int Impact,
    string Rationale) : IRequest<RiskAssessmentView>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
