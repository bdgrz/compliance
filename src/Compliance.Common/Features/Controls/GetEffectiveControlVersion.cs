using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.version.effective.get", 1)]
public sealed record GetEffectiveControlVersion(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    DateOnly EffectiveOn)
    : IRequest<ControlVersionView>, IProgramScopedRequest, ICallable;
