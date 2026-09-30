
namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     One source family considered by a run. Status is assessed or not_assessed; a family that
///     is not assessed is an explicit, acknowledged gap rather than a positive result.
/// </summary>
public sealed record ReadinessInputView(string Family, string Status, int RecordCount,
    string Explanation);
