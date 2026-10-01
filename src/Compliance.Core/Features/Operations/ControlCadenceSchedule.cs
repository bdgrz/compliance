using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Validates and describes operating cadences and derives the deterministic expected
///     occurrence periods of a recurring cadence. Occurrence identity depends only on the control
///     and the period start, so a revised plan with the same periods keeps the same occurrences.
/// </summary>
public static class ControlCadenceSchedule
{
    public const string Recurring = "recurring";
    public const string EventDriven = "event_driven";
    public const string AdHoc = "ad_hoc";
    public const int MaximumDueWithinDays = 365;

    public static string? Validate(ControlCadence? cadence)
    {
        if (cadence is null)
            return "An operating plan requires a cadence.";
        return cadence.Kind switch
        {
            Recurring when MonthsOrDays(cadence.Frequency) is null =>
                "A recurring cadence requires a frequency of weekly, monthly, quarterly, semiannual, or annual.",
            Recurring when cadence.FirstPeriodStart is null =>
                "A recurring cadence requires the first period start.",
            Recurring when cadence.DueWithinDays is not (>= 0 and <= MaximumDueWithinDays) =>
                "A recurring cadence requires due_within_days between 0 and 365.",
            Recurring when cadence.Trigger is not null =>
                "A recurring cadence cannot name a trigger.",
            EventDriven when string.IsNullOrWhiteSpace(cadence.Trigger) ||
                             cadence.Trigger.Trim().Length > 500 =>
                "An event-driven cadence requires a trigger of at most 500 characters.",
            EventDriven or AdHoc when cadence.Frequency is not null ||
                                      cadence.FirstPeriodStart is not null =>
                "Only a recurring cadence has a frequency and first period start.",
            AdHoc when cadence.Trigger is not null => "An ad hoc cadence cannot name a trigger.",
            Recurring or EventDriven or AdHoc when cadence.DueWithinDays is < 0 or
                > MaximumDueWithinDays => "due_within_days must be between 0 and 365.",
            Recurring or EventDriven or AdHoc => null,
            _ => "A cadence kind must be recurring, event_driven, or ad_hoc.",
        };
    }

    public static ControlCadence Clean(ControlCadence cadence) =>
        cadence with { Trigger = cadence.Trigger?.Trim() };

    /// <summary>Describes a cadence in user language rather than scheduler syntax.</summary>
    public static string Describe(ControlCadence cadence)
    {
        ArgumentNullException.ThrowIfNull(cadence);
        var due = cadence.DueWithinDays is { } days
            ? string.Create(CultureInfo.InvariantCulture,
                $", due {days} {(days == 1 ? "day" : "days")} after")
            : string.Empty;
        return cadence.Kind switch
        {
            Recurring => string.Create(CultureInfo.InvariantCulture,
                $"Every {Noun(cadence.Frequency)} starting {cadence.FirstPeriodStart:yyyy-MM-dd}{due}{(due.Length > 0 ? " the period ends" : string.Empty)}"),
            EventDriven => $"Whenever {cadence.Trigger}{(due.Length > 0 ? due + " the event" : string.Empty)}",
            _ => "As needed (ad hoc)" + (due.Length > 0 ? due + " it is opened" : string.Empty),
        };
    }

    public static Uuid OccurrenceId(Uuid controlId, DateOnly periodStart) =>
        Uuid.CreateVersion5(controlId, "control-occurrence-" +
            periodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    /// <summary>
    ///     Expected periods whose start lies in [from, until) and on or before <paramref name="through" />.
    /// </summary>
    public static IEnumerable<ExpectedPeriod> Periods(ControlCadence cadence, DateOnly from,
        DateOnly? until, DateOnly through)
    {
        ArgumentNullException.ThrowIfNull(cadence);
        if (cadence.Kind != Recurring || cadence.FirstPeriodStart is not { } first ||
            MonthsOrDays(cadence.Frequency) is not { } step)
            yield break;
        for (var index = 0; index < 10_000; index++)
        {
            var start = Advance(first, step, index);
            if (start > through || until is { } end && start >= end)
                yield break;
            if (start < from)
                continue;
            var periodEnd = Advance(first, step, index + 1).AddDays(-1);
            yield return new ExpectedPeriod(start, periodEnd,
                periodEnd.AddDays(cadence.DueWithinDays ?? 0));
        }
    }

    static DateOnly Advance(DateOnly first, (int Months, int Days) step, int count) =>
        step.Months > 0 ? first.AddMonths(step.Months * count) : first.AddDays(step.Days * count);

    static (int Months, int Days)? MonthsOrDays(string? frequency) => frequency switch
    {
        "weekly" => (0, 7),
        "monthly" => (1, 0),
        "quarterly" => (3, 0),
        "semiannual" => (6, 0),
        "annual" => (12, 0),
        _ => null,
    };

    static string Noun(string? frequency) => frequency switch
    {
        "weekly" => "week",
        "monthly" => "month",
        "quarterly" => "quarter",
        "semiannual" => "six months",
        _ => "year",
    };
}
