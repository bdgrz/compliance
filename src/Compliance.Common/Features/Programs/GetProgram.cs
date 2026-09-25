using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.program.get", 1)]
public sealed record GetProgram(Uuid TenantId, Uuid ProgramId, long? MinimumRevision = null)
    : IRequest<ProgramView>, ITenantAccessRequest, ICallable;
