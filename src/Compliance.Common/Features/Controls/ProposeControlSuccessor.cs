using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.successor.propose", 1)]
public sealed record ProposeControlSuccessor(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid ExpectedApprovedVersionId, ControlDraftContent Content)
    : IRequest<ControlSuccessorRegistration>, IProgramScopedRequest, ICallable;
