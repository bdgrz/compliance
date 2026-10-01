using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Lists active controls whose ownership, cadence, or expected work is blocking.</summary>
[Discriminator("bdgrz.control.operating_blockers.list", 1)]
public sealed record ListControlOperatingBlockers(Uuid TenantId, Uuid ProgramId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<ControlOperatingBlockerView>>, IProgramReadRequest, ICallable;
