using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.operations.get-control-calendar", 1)]
public sealed record GetControlCalendar(Uuid TenantId, Uuid ProgramId, DateOnly FromDate, DateOnly ThroughDate)
    : IRequest<ControlCalendarView>, IProgramReadRequest, ICallable;
