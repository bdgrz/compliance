using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.draft.create", 1)]
public sealed record CreateControlDraft(Uuid TenantId, Uuid ProgramId, string Identifier,
    ControlDraftContent Content) : IRequest<ControlRegistration>, IProgramManagementRequest, ICallable;
