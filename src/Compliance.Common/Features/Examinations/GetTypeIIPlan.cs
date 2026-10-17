using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.examinations.get-type-i-i-plan", 1)]
public sealed record GetTypeIIPlan(Uuid TenantId, Uuid ProgramId, Uuid TypeIIPlanId)
    : IRequest<TypeIIPlanView>, IProgramReadRequest, ICallable;
