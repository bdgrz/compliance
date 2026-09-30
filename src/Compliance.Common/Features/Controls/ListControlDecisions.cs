using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.decisions.list", 1)]
public sealed record ListControlDecisions(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<ControlDecisionView>>, IProgramScopedRequest, ICallable;
