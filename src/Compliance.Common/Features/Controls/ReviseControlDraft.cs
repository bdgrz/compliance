using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.draft.revise", 1)]
public sealed record ReviseControlDraft(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long ExpectedRevision, ControlDraftContent Content) : IRequest,
    IProgramManagementRequest, ICallable;
