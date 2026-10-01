namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     How often a control operates. Kind is recurring, event_driven, or ad_hoc. A recurring cadence
///     needs a frequency (weekly, monthly, quarterly, semiannual, or annual), the first period
///     start, and the days allowed after a period ends; an event-driven cadence names its trigger.
/// </summary>
public sealed record ControlCadence(string Kind, string? Frequency = null,
    DateOnly? FirstPeriodStart = null, int? DueWithinDays = null, string? Trigger = null);
