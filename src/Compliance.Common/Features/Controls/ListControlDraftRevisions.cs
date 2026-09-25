using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.draft.revisions.list", 1)]
public sealed record ListControlDraftRevisions(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    int? Limit = null, string? Cursor = null, long? MinimumControlDraftRevision = null)
    : IRequest<Page<ControlDraftRevisionView>>, IProgramManagementRequest, ICallable;
