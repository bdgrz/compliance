using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Lists a control's evaluations, newest first, optionally filtered by state.</summary>
[Discriminator("bdgrz.control.evaluations.list", 1)]
public sealed record ListControlEvaluations(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    string? State = null, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ControlEvaluationView>>, IProgramReadRequest, ICallable;
