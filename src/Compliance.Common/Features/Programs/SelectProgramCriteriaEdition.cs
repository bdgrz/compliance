using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.program.criteria.select", 1)]
public sealed record SelectProgramCriteriaEdition(Uuid TenantId, Uuid ProgramId,
    long ExpectedRevision, Uuid EditionId) : IRequest, IProgramManagementRequest, ICallable;
