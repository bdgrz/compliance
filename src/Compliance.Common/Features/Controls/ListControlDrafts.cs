using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.draft.list", 1)]
public sealed record ListControlDrafts(Uuid TenantId, Uuid ProgramId, int? Limit = null,
    string? Cursor = null) : IRequest<Page<ControlDraftView>>, IProgramManagementRequest, ICallable;
