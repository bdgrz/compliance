using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Lists a control's recorded occurrences and its expected occurrences through a horizon.</summary>
[Discriminator("bdgrz.control.occurrences.list", 1)]
public sealed record ListControlOccurrences(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    string? State = null, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ControlOccurrenceView>>, IProgramReadRequest, ICallable;
