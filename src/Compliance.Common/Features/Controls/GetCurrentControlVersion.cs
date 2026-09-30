using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.version.current.get", 1)]
public sealed record GetCurrentControlVersion(Uuid TenantId, Uuid ProgramId, Uuid ControlId)
    : IRequest<ControlVersionView>, IProgramScopedRequest, ICallable;
