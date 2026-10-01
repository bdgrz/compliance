using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>One planned control's current plan, display identifier, and occurrences through a horizon.</summary>
public sealed record PlannedControlOccurrences(Uuid ControlId, ControlOperatingPlanView Plan,
    string Identifier, IReadOnlyList<ControlOccurrenceView> Occurrences);
