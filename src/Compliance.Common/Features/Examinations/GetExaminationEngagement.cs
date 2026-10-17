using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.examinations.get-examination-engagement", 1)]
public sealed record GetExaminationEngagement(Uuid TenantId, Uuid ProgramId, Uuid ExaminationEngagementId)
    : IRequest<ExaminationEngagementView>, IProgramReadRequest, ICallable;
