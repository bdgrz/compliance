using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.program.revise", 1)]
public sealed record ReviseProgram(Uuid TenantId, Uuid ProgramId, long ExpectedRevision,
    string Name, ProgramPlan Plan) : IRequest, IProgramManagementRequest, ICallable;
