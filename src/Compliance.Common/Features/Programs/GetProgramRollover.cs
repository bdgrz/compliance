using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.programs.get-program-rollover", 1)]
public sealed record GetProgramRollover(Uuid TenantId, Uuid ProgramId, Uuid ProgramRolloverId)
    : IRequest<ProgramRolloverView>, IProgramReadRequest, ICallable;
