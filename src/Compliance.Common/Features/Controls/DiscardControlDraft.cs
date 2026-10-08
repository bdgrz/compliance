using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.draft.discard", 1)]
public sealed record DiscardControlDraft(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long ExpectedRevision, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
