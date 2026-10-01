using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Bounds one owning context's affected records for a Control impact preview.</summary>
static class ControlImpactRecords
{
    public const int Maximum = 200;

    /// <summary>
    ///     A complete contribution, or a pending one when the context names more records than a
    ///     preview may carry; a pending context blocks the decision rather than hiding records.
    /// </summary>
    public static Result<ControlImpactContribution> Contribution(string context,
        IReadOnlyCollection<ControlAffectedRecord> records) =>
        Result<ControlImpactContribution>.Success(records.Count <= Maximum
            ? new ControlImpactContribution(context, "complete", "authoritative_source",
                [.. records], true)
            : new ControlImpactContribution(context, "pending", "bounded_preview_exceeded",
                [.. records.Take(Maximum)], false));
}
