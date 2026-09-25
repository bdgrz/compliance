using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.program.revision.get", 1)]
public sealed record GetProgramRevision(Uuid TenantId, Uuid ProgramId, long Revision)
    : IRequest<ProgramRevisionView>, ITenantAccessRequest, ICallable;
