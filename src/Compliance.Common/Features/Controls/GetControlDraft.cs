using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.draft.get", 1)]
public sealed record GetControlDraft(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long? MinimumRevision = null) : IRequest<ControlDraftView>, IProgramManagementRequest, ICallable;
