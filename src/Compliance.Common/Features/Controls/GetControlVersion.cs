using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.version.get", 1)]
public sealed record GetControlVersion(Uuid TenantId, Uuid ProgramId, Uuid ControlId, Uuid VersionId)
    : IRequest<ControlVersionView>, IProgramScopedRequest, ICallable;
