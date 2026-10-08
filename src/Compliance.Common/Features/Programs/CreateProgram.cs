using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.program.create", 1)]
public sealed record CreateProgram(Uuid TenantId, string Name, ProgramPlan Plan)
    : IRequest<ProgramRegistration>, IProgramManagementRequest, IClientManagementMutationRequest, ICallable;
