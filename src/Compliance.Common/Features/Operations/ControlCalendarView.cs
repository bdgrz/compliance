using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Due control occurrences over the requested range and their source schedules.</summary>
public sealed record ControlCalendarView(Uuid TenantId, Uuid ProgramId, DateOnly FromDate, DateOnly ThroughDate, IReadOnlyList<ControlOccurrenceView> Entries);
