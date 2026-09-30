namespace Bdgrz.Compliance.Features.Controls;

/// <param name="Status">
///     complete, pending, or unlinked (no owning source that could reference a Control exists
///     in this build).
/// </param>
/// <param name="Freshness">How the contributor read its source, such as authoritative_source.</param>
public sealed record ControlImpactContribution(string Context, string Status, string Freshness,
    IReadOnlyList<ControlAffectedRecord> Records, bool Complete);
