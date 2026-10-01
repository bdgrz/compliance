using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

[Discriminator("bdgrz.control.occurrence.get", 1)]
public sealed record GetControlOccurrence(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid OccurrenceId)
    : IRequest<ControlOccurrenceView>, IProgramReadRequest, ICallable;
