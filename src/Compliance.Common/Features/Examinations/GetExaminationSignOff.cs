using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.examinations.get-examination-sign-off", 1)]
public sealed record GetExaminationSignOff(Uuid TenantId, Uuid ProgramId, Uuid ExaminationSignOffId)
    : IRequest<ExaminationSignOffView>, IProgramReadRequest, ICallable;
