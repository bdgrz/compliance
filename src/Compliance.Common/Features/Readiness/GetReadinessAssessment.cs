using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

[Discriminator("bdgrz.readiness.assessment.get", 1)]
public sealed record GetReadinessAssessment(Uuid TenantId, Uuid ProgramId, Uuid AssessmentId)
    : IRequest<ReadinessAssessmentView>, IProgramReadRequest, ICallable;
